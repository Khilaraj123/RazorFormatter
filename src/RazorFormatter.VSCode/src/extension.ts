import * as vscode from 'vscode';
import { spawn } from 'child_process';
import { resolveCli } from './resolver';
import { FormatterServerClient, ServerFormatResult, ServerTextChange } from './serverClient';

let outputChannel: vscode.OutputChannel;
let serverClient: FormatterServerClient | null = null;

export function activate(context: vscode.ExtensionContext) {
    outputChannel = vscode.window.createOutputChannel('Razor Formatter');
    context.subscriptions.push(outputChannel);

    outputChannel.appendLine('Razor Formatter extension activated.');

    const selector: vscode.DocumentSelector = [
        { language: 'aspnetcorerazor' },
        { language: 'razor' },
        { scheme: 'file', pattern: '**/*.cshtml' },
        { scheme: 'file', pattern: '**/*.razor' }
    ];

    // Initialize server client if enabled
    const config = vscode.workspace.getConfiguration('razorFormatter');
    const useServer = config.get<boolean>('serverMode', true);

    if (useServer) {
        try {
            const resolution = resolveCli(context);
            serverClient = new FormatterServerClient(resolution, outputChannel);
            serverClient.start().catch(err => {
                outputChannel.appendLine(`[Warning] Could not pre-start formatting server: ${err.message}. Will retry on format request.`);
            });
            context.subscriptions.push(serverClient);
        } catch (err: any) {
            outputChannel.appendLine(`[Warning] Failed to initialize server: ${err.message}`);
        }
    }

    // Document Formatting Provider (Full Document & Format-on-Save)
    const docProvider: vscode.DocumentFormattingEditProvider = {
        async provideDocumentFormattingEdits(
            document: vscode.TextDocument,
            options: vscode.FormattingOptions,
            token: vscode.CancellationToken
        ): Promise<vscode.TextEdit[]> {
            const startTime = Date.now();
            try {
                const edits = await formatDocument(context, document, options, token);
                const elapsed = Date.now() - startTime;
                outputChannel.appendLine(`[Format] ${document.fileName}: formatted in ${elapsed}ms (${edits.length} edits)`);
                return edits;
            } catch (err: any) {
                const elapsed = Date.now() - startTime;
                outputChannel.appendLine(`[Error] ${document.fileName}: failed after ${elapsed}ms: ${err.message}`);
                return [];
            }
        }
    };

    // Range Formatting Provider (Selection formatting)
    const rangeProvider: vscode.DocumentRangeFormattingEditProvider = {
        async provideDocumentRangeFormattingEdits(
            document: vscode.TextDocument,
            range: vscode.Range,
            options: vscode.FormattingOptions,
            token: vscode.CancellationToken
        ): Promise<vscode.TextEdit[]> {
            const startTime = Date.now();
            try {
                const edits = await formatRange(context, document, range, options, token);
                const elapsed = Date.now() - startTime;
                outputChannel.appendLine(`[RangeFormat] ${document.fileName}: formatted in ${elapsed}ms (${edits.length} edits)`);
                return edits;
            } catch (err: any) {
                const elapsed = Date.now() - startTime;
                outputChannel.appendLine(`[Error] ${document.fileName}: range format failed after ${elapsed}ms: ${err.message}`);
                return [];
            }
        }
    };

    context.subscriptions.push(
        vscode.languages.registerDocumentFormattingEditProvider(selector, docProvider),
        vscode.languages.registerDocumentRangeFormattingEditProvider(selector, rangeProvider)
    );
}

export function deactivate() {
    serverClient?.dispose();
    serverClient = null;
}

async function formatDocument(
    context: vscode.ExtensionContext,
    document: vscode.TextDocument,
    options: vscode.FormattingOptions,
    token: vscode.CancellationToken
): Promise<vscode.TextEdit[]> {
    const config = vscode.workspace.getConfiguration('razorFormatter', document.uri);
    const useServer = config.get<boolean>('serverMode', true);
    const text = document.getText();
    const filePath = document.uri.scheme === 'file' ? document.uri.fsPath : undefined;

    const formatOpts = {
        indentSize: options.tabSize,
        indentStyle: options.insertSpaces ? 'space' : 'tab',
        attributeLayout: config.get<string>('attributeLayout')
    };

    if (useServer && serverClient) {
        try {
            const result = await serverClient.format(text, filePath, formatOpts, token);
            return convertResultToEdits(document, result);
        } catch (serverErr: any) {
            outputChannel.appendLine(`[Server Fallback] Server format error: ${serverErr.message}. Falling back to CLI process.`);
        }
    }

    // Fallback to one-shot CLI execution
    return runOneShotCli(context, document, text, filePath, formatOpts, token);
}

async function formatRange(
    context: vscode.ExtensionContext,
    document: vscode.TextDocument,
    range: vscode.Range,
    options: vscode.FormattingOptions,
    token: vscode.CancellationToken
): Promise<vscode.TextEdit[]> {
    const config = vscode.workspace.getConfiguration('razorFormatter', document.uri);
    const useServer = config.get<boolean>('serverMode', true);
    const text = document.getText();
    const filePath = document.uri.scheme === 'file' ? document.uri.fsPath : undefined;
    const startOffset = document.offsetAt(range.start);
    const endOffset = document.offsetAt(range.end);

    const formatOpts = {
        indentSize: options.tabSize,
        indentStyle: options.insertSpaces ? 'space' : 'tab',
        attributeLayout: config.get<string>('attributeLayout')
    };

    if (useServer && serverClient) {
        try {
            const result = await serverClient.formatRange(text, startOffset, endOffset, filePath, formatOpts, token);
            return convertResultToEdits(document, result);
        } catch (serverErr: any) {
            outputChannel.appendLine(`[Server Fallback] Server range format error: ${serverErr.message}. Falling back to CLI process.`);
        }
    }

    // For range formatting fallback: format document and filter edits
    const allEdits = await runOneShotCli(context, document, text, filePath, formatOpts, token);
    return allEdits.filter(edit => {
        return !edit.range.end.isBefore(range.start) && !edit.range.start.isAfter(range.end);
    });
}

function convertResultToEdits(document: vscode.TextDocument, result: ServerFormatResult): vscode.TextEdit[] {
    if (result.wasSkipped || !result.changes || result.changes.length === 0) {
        return [];
    }

    const edits: vscode.TextEdit[] = [];
    for (const ch of result.changes) {
        const startPos = document.positionAt(ch.start);
        const endPos = document.positionAt(ch.end);
        edits.push(vscode.TextEdit.replace(new vscode.Range(startPos, endPos), ch.newText));
    }

    return edits;
}

function runOneShotCli(
    context: vscode.ExtensionContext,
    document: vscode.TextDocument,
    text: string,
    filePath: string | undefined,
    options: { indentSize?: number; indentStyle?: string; attributeLayout?: string },
    token: vscode.CancellationToken
): Promise<vscode.TextEdit[]> {
    return new Promise((resolve, reject) => {
        const resolution = resolveCli(context, document.uri);
        const args = [...resolution.args, '--changes-json'];

        if (options.indentStyle === 'tab') {
            args.push('--tab');
        } else if (options.indentSize) {
            args.push('--indent', options.indentSize.toString());
        }

        if (options.attributeLayout) {
            args.push('--attribute-layout', options.attributeLayout);
        }

        if (filePath) {
            args.push('--path', filePath);
        }

        const child = spawn(resolution.command, args, { shell: false });

        // Process cancellation: terminate the child process immediately
        const cancelListener = token.onCancellationRequested(() => {
            try {
                child.kill('SIGKILL');
            } catch {
                // Process may already be dead
            }
            resolve([]);
        });

        let stdout = '';
        let stderr = '';

        child.stdout?.on('data', chunk => {
            stdout += chunk.toString();
        });

        child.stderr?.on('data', chunk => {
            stderr += chunk.toString();
        });

        child.on('error', err => {
            cancelListener.dispose();
            outputChannel.appendLine(`[CLI Process Error] ${err.message}`);
            reject(err);
        });

        child.on('close', code => {
            cancelListener.dispose();

            if (token.isCancellationRequested) {
                return resolve([]);
            }

            if (code !== 0) {
                outputChannel.appendLine(`[CLI Exit ${code}] ${stderr}`);
                return resolve([]);
            }

            try {
                const parsed = JSON.parse(stdout.trim());
                const serverResult: ServerFormatResult = {
                    formattedText: parsed.formattedText || '',
                    wasSkipped: parsed.wasSkipped || false,
                    changes: (parsed.changes || []).map((c: any) => ({
                        start: c.Start !== undefined ? c.Start : c.start,
                        end: c.End !== undefined ? c.End : c.end,
                        newText: c.NewText !== undefined ? c.NewText : c.newText
                    })),
                    diagnostics: parsed.diagnostics || []
                };

                resolve(convertResultToEdits(document, serverResult));
            } catch (err: any) {
                outputChannel.appendLine(`[CLI Parse Error] Failed to parse JSON edits: ${err.message}`);
                resolve([]);
            }
        });

        child.stdin?.write(text);
        child.stdin?.end();
    });
}
