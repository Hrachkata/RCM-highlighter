const vscode = require('vscode');
const path = require('path');

const {
	LanguageClient
} = require('vscode-languageclient/node');

const {
	updateDiagnostics,
	dontValidate,
	refreshEslintConfig
} = require('./ESLintValidator');

const {
	getJsCompletions
} = require('./CompletionService');

const {
	JsVirtualDocumentProvider
} = require('./JsVirtualDocumentProvider');


const languageServerPathDebug = "server/bin/Debug/net5.0/RcmServer.dll";
const languageServerPathRelease = "server/bin/Release/net5.0/RcmServer.dll";
const languageServerDll = "RcmServer.dll";
const jsScheme = 'js-in-xml';

//virtualProvider = new JsVirtualDocumentProvider();

function activate(context) {
	let workPathDebug = path.dirname(context.asAbsolutePath(languageServerPathDebug));
	let workPathRelease = path.dirname(context.asAbsolutePath(languageServerPathRelease));

	let serverOptions = {
		run: {
			command: "dotnet",
			args: [languageServerDll],
			options: {
				cwd: workPathRelease
			}
		},
		debug: {
			command: "dotnet",
			args: [languageServerDll, "--debug"],
			options: {
				cwd: workPathDebug
			}
		}
	};

	let clientOptions = {
		documentSelector: [
			{
				scheme: 'file',
				language: 'rcm'
			}],
		synchronize: {
			fileEvents: vscode.workspace.createFileSystemWatcher('**/.clientrc')
		}
	};

	let triggerTimeout;

	// Completion handler for templates hack
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
				vscode.commands.executeCommand('editor.action.triggerSuggest', { auto: true });
			}
		}, 300); // Small delay to ensure cursor position is updated
	});

	let client = new LanguageClient(
		'xmljs-rcm-highlight',
		'XML JS RCM Language Client',
		serverOptions,
		clientOptions
	);

	context.subscriptions.push(client);


	// Diagnostics (Error Checking)
	const diagnostics = vscode.languages.createDiagnosticCollection(jsScheme);
	context.subscriptions.push(diagnostics);

	// OnChange sent to ESLint
	let lintTimeout;
	context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(e => {
		clearTimeout(lintTimeout);
		lintTimeout = setTimeout(() => updateDiagnostics(e.document, diagnostics), 500);
	}));

	context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(doc => {
		setTimeout(() => updateDiagnostics(doc, diagnostics), 500);
	}));

	// Completion Provider JS
	context.subscriptions.push(
		vscode.languages.registerCompletionItemProvider(
			'rcm',
			{
				async provideCompletionItems(document, position, token, context) {
					// Both properties and functions are accessed through a "." symbol
					let isPotentialFunctionCall = false;
					if (context) {
						if (context.triggerCharacter == '.') {
							isPotentialFunctionCall = true;
						}
					}

					let completions = await getJsCompletions(document, position, isPotentialFunctionCall);

					if (completions) {
						return completions;
					}
				}
			},
			'.'
		)
	);

	// TODO: XML Formatter, low priority
	// vscode.languages.registerDocumentFormattingEditProvider('rcm', {
	// 	async provideDocumentFormattingEdits(document) {
	// 		const edits = await vscode.commands.executeCommand<vscode.TextEdit>(
	// 			'vscode.executeFormatDocumentProvider',
	// 			document.uri
	// 		);
	// 		return edits;
	// 	}
	// });

	client.start();
}

module.exports = {
	activate,
	deactivate: () => {
		client ? client.stop() : undefined;
	}
};