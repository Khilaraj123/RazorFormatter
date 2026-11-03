import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { execSync } from 'child_process';

export interface CliResolution {
    command: string;
    args: string[];
    isDirectExecutable: boolean;
}

export function resolveCli(context: vscode.ExtensionContext, documentUri?: vscode.Uri): CliResolution {
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
        try {
            const whichCmd = process.platform === 'win32' ? 'where razor-format' : 'which razor-format';
            const result = execSync(whichCmd, { stdio: ['ignore', 'pipe', 'ignore'], timeout: 2000 }).toString().trim().split('\n')[0].trim();
            if (result && fs.existsSync(result)) {
                return { command: result, args: [], isDirectExecutable: true };
            }
        } catch {
            // Not found on PATH
        }
    }

    // 4. Safe development fallback: only if the current workspace root is the RazorFormatter repo itself
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

    // Default fallback: global 'razor-format' tool
    return {
        command: 'razor-format',
        args: [],
        isDirectExecutable: true
    };
}
