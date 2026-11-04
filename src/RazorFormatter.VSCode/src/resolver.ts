import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
import { execSync, exec } from 'child_process';

export interface CliResolution {
    command: string;
    args: string[];
    isDirectExecutable: boolean;
}

/**
 * Searches for an existing RazorFormatter CLI resolution across configuration,
 * bundled extension bin directory, system PATH, ~/.dotnet/tools, and workspace repository.
 * Returns null if no valid CLI could be located.
 */
export function findResolvedCli(context: vscode.ExtensionContext, documentUri?: vscode.Uri): CliResolution | null {
    const config = vscode.workspace.getConfiguration('razorFormatter', documentUri);
    const customCli = config.get<string>('cliPath');

    // 1. Explicitly configured CLI path (must be set in Machine/User settings)
    if (customCli && customCli.trim().length > 0) {
        const trimmed = customCli.trim();
        if (fs.existsSync(trimmed)) {
            if (trimmed.endsWith('.dll')) {
                return { command: 'dotnet', args: [trimmed], isDirectExecutable: false };
            }
            return { command: trimmed, args: [], isDirectExecutable: true };
        }
    }

    // 2. Extension bundle directory (e.g. distributed binaries in bin/)
    const extensionBinExe = path.join(context.extensionPath, 'bin', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli');
    if (fs.existsSync(extensionBinExe)) {
        return { command: extensionBinExe, args: [], isDirectExecutable: true };
    }

    const extensionBinDll = path.join(context.extensionPath, 'bin', 'RazorFormatter.Cli.dll');
    if (fs.existsSync(extensionBinDll)) {
        return { command: 'dotnet', args: [extensionBinDll], isDirectExecutable: false };
    }

    // 3. Check global tool on PATH (only in trusted workspaces)
    if (vscode.workspace.isTrusted) {
        for (const candidate of ['razorfmt', 'razor-format']) {
            try {
                const whichCmd = process.platform === 'win32' ? `where ${candidate}` : `which ${candidate}`;
                const result = execSync(whichCmd, { stdio: ['ignore', 'pipe', 'ignore'], timeout: 2000 }).toString().trim().split('\n')[0].trim();
                if (result && fs.existsSync(result)) {
                    return { command: result, args: [], isDirectExecutable: true };
                }
            } catch {
                // Not found on PATH
            }
        }

        // 4. Check default ~/.dotnet/tools directory directly (in case PATH hasn't been refreshed in VS Code)
        const userToolsDir = path.join(os.homedir(), '.dotnet', 'tools');
        for (const candidate of ['razorfmt', 'razor-format']) {
            const toolExe = path.join(userToolsDir, process.platform === 'win32' ? `${candidate}.exe` : candidate);
            if (fs.existsSync(toolExe)) {
                return { command: toolExe, args: [], isDirectExecutable: true };
            }
        }
    }

    // 5. Safe development fallback: only if the current workspace root is the RazorFormatter repo itself
    if (vscode.workspace.isTrusted) {
        const workspaceFolders = vscode.workspace.workspaceFolders || [];
        for (const folder of workspaceFolders) {
            const slnPath = path.join(folder.uri.fsPath, 'RazorFormatter.slnx');
            const projPath = path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'RazorFormatter.Cli.csproj');
            if (fs.existsSync(slnPath) && fs.existsSync(projPath)) {
                return {
                    command: 'dotnet',
                    args: ['run', '--no-build', '--project', projPath, '--'],
                    isDirectExecutable: false
                };
            }
        }
    }

    return null;
}

/**
 * Checks whether the 'dotnet' CLI runtime is available on the system PATH.
 */
export function isDotnetAvailable(): boolean {
    try {
        const checkCmd = process.platform === 'win32' ? 'where dotnet' : 'which dotnet';
        const result = execSync(checkCmd, { stdio: ['ignore', 'pipe', 'ignore'], timeout: 2000 }).toString().trim();
        return result.length > 0;
    } catch {
        return false;
    }
}

/**
 * Resolves the CLI or falls back to 'razorfmt' (backward-compatible).
 */
export function resolveCli(context: vscode.ExtensionContext, documentUri?: vscode.Uri): CliResolution {
    const resolved = findResolvedCli(context, documentUri);
    if (resolved) {
        return resolved;
    }

    // Default fallback: global 'razorfmt' tool
    return {
        command: 'razorfmt',
        args: [],
        isDirectExecutable: true
    };
}

let isInstallingTool = false;

/**
 * Ensures that the RazorFormatter CLI is available on the machine.
 * If not present, automatically runs 'dotnet tool install -g RazorCodeFormatter'
 * with progress notification in VS Code.
 */
export async function ensureCliInstalled(
    context: vscode.ExtensionContext,
    outputChannel: vscode.OutputChannel,
    documentUri?: vscode.Uri
): Promise<CliResolution | null> {
    const existing = findResolvedCli(context, documentUri);
    if (existing) {
        return existing;
    }

    if (isInstallingTool) {
        // Wait for active install in progress
        for (let i = 0; i < 20; i++) {
            await new Promise(r => setTimeout(r, 500));
            const recheck = findResolvedCli(context, documentUri);
            if (recheck) return recheck;
            if (!isInstallingTool) break;
        }
    }

    if (!isDotnetAvailable()) {
        outputChannel.appendLine('[Tool Installer] .NET SDK / runtime not found on system PATH.');
        const action = await vscode.window.showErrorMessage(
            'Razor Formatter: .NET SDK or runtime was not found. Please install .NET to run Razor Formatter.',
            'Download .NET'
        );
        if (action === 'Download .NET') {
            vscode.env.openExternal(vscode.Uri.parse('https://dotnet.microsoft.com/download'));
        }
        return null;
    }

    isInstallingTool = true;
    try {
        outputChannel.appendLine('[Tool Installer] RazorFormatter CLI not found. Automatically installing RazorCodeFormatter .NET global tool from NuGet...');

        const success = await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'Razor Formatter: Installing RazorCodeFormatter (.NET tool)...',
            cancellable: false
        }, async (progress) => {
            progress.report({ message: 'Running dotnet tool install -g RazorCodeFormatter...' });

            return new Promise<boolean>((resolve) => {
                exec('dotnet tool install -g RazorCodeFormatter', (error, stdout, stderr) => {
                    if (stdout) outputChannel.appendLine(`[Tool Installer Output] ${stdout.trim()}`);
                    if (stderr) outputChannel.appendLine(`[Tool Installer Stderr] ${stderr.trim()}`);

                    if (error) {
                        outputChannel.appendLine(`[Tool Installer Error] Failed to install: ${error.message}`);
                        resolve(false);
                    } else {
                        resolve(true);
                    }
                });
            });
        });

        if (success) {
            outputChannel.appendLine('[Tool Installer] RazorCodeFormatter tool installed successfully.');
            vscode.window.showInformationMessage('Razor Formatter: RazorCodeFormatter tool installed successfully.');
            return findResolvedCli(context, documentUri);
        } else {
            const recheck = findResolvedCli(context, documentUri);
            if (recheck) {
                return recheck;
            }

            const choice = await vscode.window.showWarningMessage(
                'Razor Formatter: Could not automatically install the RazorCodeFormatter tool. Please ensure you have internet access or install it manually using "dotnet tool install -g RazorCodeFormatter".',
                'View Output'
            );
            if (choice === 'View Output') {
                outputChannel.show();
            }
            return null;
        }
    } finally {
        isInstallingTool = false;
    }
}

/**
 * Explicitly installs or updates the RazorCodeFormatter global tool.
 */
export async function installOrUpdateCli(
    context: vscode.ExtensionContext,
    outputChannel: vscode.OutputChannel
): Promise<CliResolution | null> {
    if (!isDotnetAvailable()) {
        const action = await vscode.window.showErrorMessage(
            'Razor Formatter: .NET SDK or runtime was not found. Please install .NET to run Razor Formatter.',
            'Download .NET'
        );
        if (action === 'Download .NET') {
            vscode.env.openExternal(vscode.Uri.parse('https://dotnet.microsoft.com/download'));
        }
        return null;
    }

    return await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Razor Formatter: Installing or updating RazorCodeFormatter tool...',
        cancellable: false
    }, async (progress) => {
        progress.report({ message: 'Running dotnet tool update -g RazorCodeFormatter...' });

        return new Promise<CliResolution | null>((resolve) => {
            exec('dotnet tool update -g RazorCodeFormatter', (updateErr, updateStdout, updateStderr) => {
                if (updateStdout) outputChannel.appendLine(`[Tool Installer] ${updateStdout.trim()}`);

                if (!updateErr) {
                    outputChannel.appendLine('[Tool Installer] RazorCodeFormatter tool updated successfully.');
                    vscode.window.showInformationMessage('Razor Formatter: RazorCodeFormatter tool updated successfully.');
                    resolve(findResolvedCli(context));
                    return;
                }

                // If update failed (e.g. not installed yet), try install
                progress.report({ message: 'Tool not installed yet, running dotnet tool install...' });
                exec('dotnet tool install -g RazorCodeFormatter', (installErr, installStdout, installStderr) => {
                    if (installStdout) outputChannel.appendLine(`[Tool Installer] ${installStdout.trim()}`);
                    if (installStderr) outputChannel.appendLine(`[Tool Installer] ${installStderr.trim()}`);

                    if (!installErr) {
                        outputChannel.appendLine('[Tool Installer] RazorCodeFormatter tool installed successfully.');
                        vscode.window.showInformationMessage('Razor Formatter: RazorCodeFormatter tool installed successfully.');
                        resolve(findResolvedCli(context));
                    } else {
                        outputChannel.appendLine(`[Tool Installer Error] Install failed: ${installErr.message}`);
                        vscode.window.showErrorMessage('Razor Formatter: Failed to install or update tool. See Razor Formatter output.');
                        resolve(null);
                    }
                });
            });
        });
    });
}
