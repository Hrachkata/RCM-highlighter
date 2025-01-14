const vscode = require('vscode');
const path = require('path');

const { LanguageClient, LanguageClientOptions, ServerOptions } = require('vscode-languageclient/node');

// Defines the search path of your language server DLL. (.NET Core)
const languageServerPath = "server/bin/Debug/net5.0/RcmServer.dll";
const languageServerDll = "RcmServer.dll";

function activate(context) {
    let workPath = path.dirname(context.asAbsolutePath(languageServerPath));

    let serverOptions = {
        run: { command: "dotnet", args: [languageServerDll], options: { cwd: workPath } },
        debug: { command: "dotnet", args: [languageServerDll, "--debug"], options: { cwd: workPath } }
    };
    

    let clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'xml' }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/.clientrc')
        }
    };

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
