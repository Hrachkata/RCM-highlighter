const vscode = require('vscode');
const path = require('path');

const { LanguageClient, LanguageClientOptions, ServerOptions } = require('vscode-languageclient/node');

// Defines the search path of your language server DLL. (.NET Core)
const languageServerPathDebug = "server/bin/Debug/net5.0/RcmServer.dll";
const languageServerPathRelease = "server/bin/Release/net5.0/RcmServer.dll";
const languageServerDll = "RcmServer.dll";

function activate(context) {
    let workPathDebug = path.dirname(context.asAbsolutePath(languageServerPathDebug));
    let workPathRelease = path.dirname(context.asAbsolutePath(languageServerPathRelease));

    let serverOptions = {
        run: { command: "dotnet", args: [languageServerDll], options: { cwd: workPathRelease } },
        debug: { command: "dotnet", args: [languageServerDll, "--debug"], options: { cwd: workPathDebug } }
    };
    

    let clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'xml' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/.clientrc')
        }
    };


    let triggerTimeout;
    vscode.workspace.onDidChangeTextDocument((event) => {
      const activeEditor = vscode.window.activeTextEditor;
      if (!activeEditor || event.document !== activeEditor.document) return;
    
      clearTimeout(triggerTimeout);
      triggerTimeout = setTimeout(() => {
        const cursorPos = activeEditor.selection.active;
        const textBeforeCursor = event.document.getText(
          new vscode.Range(cursorPos.with(undefined, 0), cursorPos)
        );

        if (!textBeforeCursor.includes('Template')) {
            return;
        } 
    
        // Check if inside quotes (attribute value)
        const isInsideQuotes = /=\s*["'][^"']*$/.test(textBeforeCursor);
        if (isInsideQuotes) {
          vscode.commands.executeCommand('editor.action.triggerSuggest');
        }
      }, 100); // Small delay to ensure cursor position is updated
    });

    let client = new LanguageClient(
        'xmljs-rcm-highlight',  // Unique id for your client
        'XML JS RCM Language Client',
        serverOptions,
        clientOptions
    );

    client.start();

    context.subscriptions.push(client);
}

module.exports = {
    activate,
    deactivate: () => client ? client.stop() : undefined
};
