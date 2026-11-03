import * as vscode from 'vscode';
import { spawn, ChildProcess } from 'child_process';
import * as readline from 'readline';
import { CliResolution } from './resolver';

export interface ServerTextChange {
    start: number;
    end: number;
    newText: string;
}

export interface ServerFormatResult {
    formattedText: string;
    wasSkipped: boolean;
    changes: ServerTextChange[];
    diagnostics: Array<{
        severity: string;
        message: string;
        offset: number;
        line: number;
        column: number;
    }>;
}

interface PendingRequest {
    resolve: (result: ServerFormatResult) => void;
    reject: (error: Error) => void;
    timer: NodeJS.Timeout;
}

export class FormatterServerClient implements vscode.Disposable {
    private process: ChildProcess | null = null;
    private nextId = 1;
    private pendingRequests = new Map<number, PendingRequest>();
    private isReady = false;
    private readyPromise: Promise<void> | null = null;
    private outputChannel: vscode.OutputChannel;
    private resolution: CliResolution;

    constructor(resolution: CliResolution, outputChannel: vscode.OutputChannel) {
        this.resolution = resolution;
        this.outputChannel = outputChannel;
    }

    public async start(): Promise<void> {
        if (this.process && !this.process.killed) {
            return;
        }

        const args = [...this.resolution.args, '--server'];
        this.outputChannel.appendLine(`[Server] Starting RazorFormatter server: ${this.resolution.command} ${args.join(' ')}`);

        this.process = spawn(this.resolution.command, args, {
            stdio: ['pipe', 'pipe', 'pipe'],
            shell: false
        });

        const rl = readline.createInterface({
            input: this.process.stdout!,
            crlfDelay: Infinity
        });

        let readyResolver: (() => void) | null = null;
        let readyRejecter: ((err: Error) => void) | null = null;
        this.readyPromise = new Promise<void>((resolve, reject) => {
            readyResolver = resolve;
            readyRejecter = reject;
        });

        rl.on('line', line => {
            this.handleLine(line, readyResolver);
        });

        this.process.stderr?.on('data', chunk => {
            this.outputChannel.appendLine(`[Server Error] ${chunk.toString().trim()}`);
        });

        this.process.on('error', err => {
            this.outputChannel.appendLine(`[Server] Process error: ${err.message}`);
            readyRejecter?.(err);
            this.cleanupPending(err);
        });

        this.process.on('close', code => {
            this.outputChannel.appendLine(`[Server] Process exited with code ${code}`);
            this.isReady = false;
            this.cleanupPending(new Error(`Server process terminated with code ${code}`));
        });

        // 5-second startup timeout
        const timeout = setTimeout(() => {
            if (!this.isReady) {
                readyRejecter?.(new Error('Server failed to start within 5000ms'));
            }
        }, 5000);

        try {
            await this.readyPromise;
        } finally {
            clearTimeout(timeout);
        }
    }

    private handleLine(line: string, onReady?: (() => void) | null): void {
        const trimmed = line.trim();
        if (trimmed.length === 0) return;

        try {
            const data = JSON.parse(trimmed);

            if (data.ready) {
                this.isReady = true;
                this.outputChannel.appendLine('[Server] Ready to accept formatting requests.');
                onReady?.();
                return;
            }

            if (data.id !== undefined && this.pendingRequests.has(data.id)) {
                const req = this.pendingRequests.get(data.id)!;
                this.pendingRequests.delete(data.id);
                clearTimeout(req.timer);

                if (data.error) {
                    req.reject(new Error(data.error));
                } else {
                    req.resolve(data.result as ServerFormatResult);
                }
            }
        } catch (err: any) {
            this.outputChannel.appendLine(`[Server] Failed to parse output line: ${line} (${err.message})`);
        }
    }

    public async format(
        text: string,
        filePath?: string,
        options?: {
            indentSize?: number;
            indentStyle?: string;
            endOfLine?: string;
            attributeLayout?: string;
        },
        token?: vscode.CancellationToken
    ): Promise<ServerFormatResult> {
        await this.ensureRunning();

        return this.sendRequest('format', {
            text,
            filePath,
            ...options
        }, token);
    }

    public async formatRange(
        text: string,
        startOffset: number,
        endOffset: number,
        filePath?: string,
        options?: {
            indentSize?: number;
            indentStyle?: string;
            endOfLine?: string;
            attributeLayout?: string;
        },
        token?: vscode.CancellationToken
    ): Promise<ServerFormatResult> {
        await this.ensureRunning();

        return this.sendRequest('formatRange', {
            text,
            startOffset,
            endOffset,
            filePath,
            ...options
        }, token);
    }

    private async sendRequest(method: string, params: any, token?: vscode.CancellationToken): Promise<ServerFormatResult> {
        if (!this.process || !this.isReady) {
            throw new Error('Formatter server is not running');
        }

        const id = this.nextId++;
        const request = {
            id,
            method,
            params
        };

        return new Promise<ServerFormatResult>((resolve, reject) => {
            if (token?.isCancellationRequested) {
                return reject(new Error('Formatting cancelled'));
            }

            const timer = setTimeout(() => {
                this.pendingRequests.delete(id);
                reject(new Error(`Formatting request timed out after 10s`));
            }, 10000);

            token?.onCancellationRequested(() => {
                this.pendingRequests.delete(id);
                clearTimeout(timer);
                reject(new Error('Formatting cancelled'));
            });

            this.pendingRequests.set(id, { resolve, reject, timer });

            try {
                this.process!.stdin!.write(JSON.stringify(request) + '\n');
            } catch (err: any) {
                this.pendingRequests.delete(id);
                clearTimeout(timer);
                reject(err);
            }
        });
    }

    private async ensureRunning(): Promise<void> {
        if (!this.process || this.process.killed || !this.isReady) {
            await this.start();
        }
    }

    private cleanupPending(err: Error): void {
        for (const [id, req] of this.pendingRequests.entries()) {
            clearTimeout(req.timer);
            req.reject(err);
        }
        this.pendingRequests.clear();
    }

    public dispose(): void {
        if (this.process && !this.process.killed) {
            try {
                this.process.stdin?.write(JSON.stringify({ id: 0, method: 'shutdown' }) + '\n');
            } catch {
                // Ignore
            }
            this.process.kill();
            this.process = null;
        }
        this.cleanupPending(new Error('Server disposed'));
    }
}

