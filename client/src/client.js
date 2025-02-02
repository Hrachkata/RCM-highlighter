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

virtualProvider = new JsVirtualDocumentProvider();

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
		documentSelector: [{
			scheme: 'file',
			language: 'xml'
		}],
		synchronize: {
			fileEvents: vscode.workspace.createFileSystemWatcher('**/.clientrc')
		}
	};

	// ----------------------------
	// Completion handler for templates hack, but to .NET srv
	// ----------------------------
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
		'xmljs-rcm-highlight', // Unique id for your client
		'XML JS RCM Language Client',
		serverOptions,
		clientOptions
	);

	client.start();

	context.subscriptions.push(client);

	// Setup virtual document provider
	context.subscriptions.push(
		vscode.workspace.registerTextDocumentContentProvider('js-in-xml', virtualProvider)
	);

	// ----------------------------
	// Diagnostics (Error Checking)
	// ----------------------------
	const diagnostics = vscode.languages.createDiagnosticCollection('js-in-xml');
	context.subscriptions.push(diagnostics);

	// OnChange sent to ESLint
	let lintTimeout;
	vscode.workspace.onDidChangeTextDocument(e => {
		clearTimeout(lintTimeout);
		lintTimeout = setTimeout(() => updateDiagnostics(e.document, diagnostics), 1000);
	});

	vscode.workspace.onDidOpenTextDocument(doc => {
		setTimeout(() => updateDiagnostics(doc, diagnostics), 1000);
	});

	// Changed active document refresh lint and clear diagnostics
	vscode.window.onDidChangeActiveTextEditor(editor => {
		if (editor) {
			const filePath = editor.document.uri.fsPath;
			let fileName = filenameRegex.exec(filePath)[0];
			dontValidate.push(fileName);
			refreshEslintConfig();
		}
	});

	const filenameRegex = /[ \w]+(?=[.])/;
	// Changed active document refresh lint and clear diagnostics
	vscode.window.onDidChangeActiveTextEditor(editor => {
		let fileNameMatch = filenameRegex.exec(vscode.window.activeTextEditor.document.fileName);
		if(fileNameMatch != null && fileNameMatch.length === 1 && !dontValidate.includes(fileName[0])){
			dontValidate.push(fileNameMatch[0]);
			refreshEslintConfig();
		}
	});

	// ----------------------------
	// Completion Provider
	// ----------------------------
	context.subscriptions.push(
		vscode.languages.registerCompletionItemProvider('xml', {
			async provideCompletionItems(document, position) {
				let completions = getJsCompletions(document, position, virtualProvider);
				return completions;
			}
		}, '=', '{') // Trigger characters
	);
}

setInterval(() => {
	const activeUris = new Set(
		vscode.window.visibleTextEditors.map(e => e.document.uri.toString())
	);
	virtualProvider.purgeInactive(activeUris);
}, 60_000);

// Cleanup every 5 minutes
setInterval(() => {
	const activeUris = new Set(
		vscode.window.visibleTextEditors
			.flatMap(editor =>
				getVirtualUrisForDocument(editor.document) // Your logic to find associated virtual URIs
			)
	);
	virtualProvider.purgeInactive(activeUris);
}, 300_000);

module.exports = {
	activate,
	deactivate: () => client ? client.stop() : undefined,
	virtualProvider
};