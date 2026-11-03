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

    // 1. Explicitly configured CLI path
    if (customCli && customCli.trim().length > 0) {
        const trimmed = customCli.trim();
        if (trimmed.endsWith('.dll')) {
            return { command: 'dotnet', args: [trimmed], isDirectExecutable: false };
        }
        return { command: trimmed, args: [], isDirectExecutable: true };
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

    // 3. Check workspace folders for pre-compiled binary
    const workspaceFolders = vscode.workspace.workspaceFolders || [];
    for (const folder of workspaceFolders) {
        const buildPaths = [
            path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
            path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Release', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
            path.join(folder.uri.fsPath, 'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
            path.join(folder.uri.fsPath, 'bin', 'Release', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli')
        ];

        for (const bp of buildPaths) {
            if (fs.existsSync(bp)) {
                return { command: bp, args: [], isDirectExecutable: true };
            }
        }

        const dllPaths = [
            path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Debug', 'net10.0', 'RazorFormatter.Cli.dll'),
            path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Release', 'net10.0', 'RazorFormatter.Cli.dll'),
            path.join(folder.uri.fsPath, 'bin', 'Debug', 'net10.0', 'RazorFormatter.Cli.dll'),
            path.join(folder.uri.fsPath, 'bin', 'Release', 'net10.0', 'RazorFormatter.Cli.dll')
        ];

        for (const dp of dllPaths) {
            if (fs.existsSync(dp)) {
                return { command: 'dotnet', args: [dp], isDirectExecutable: false };
            }
        }
    }

    // 4. Check global tool on PATH
    try {
        const whichCmd = process.platform === 'win32' ? 'where razor-format' : 'which razor-format';
        const result = execSync(whichCmd, { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim().split('\n')[0].trim();
        if (result && fs.existsSync(result)) {
            return { command: result, args: [], isDirectExecutable: true };
        }
    } catch {
        // Not found on PATH
    }

    // 5. Fallback: dotnet run against local project if in this repository
    for (const folder of workspaceFolders) {
        const projPath = path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'RazorFormatter.Cli.csproj');
        if (fs.existsSync(projPath)) {
            return {
                command: 'dotnet',
                args: ['run', '--no-build', '--project', projPath, '--'],
                isDirectExecutable: false
            };
        }
    }

    // Default fallback: expect 'razor-format' or 'dotnet'
    return {
        command: 'dotnet',
        args: ['run', '--project', 'src/RazorFormatter.Cli', '--'],
        isDirectExecutable: false
    };
}

