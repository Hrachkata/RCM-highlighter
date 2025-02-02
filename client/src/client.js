const vscode = require('vscode');
const path = require('path');

const {
	ESLint
} = require('eslint');

const {
	LanguageClient
} = require('vscode-languageclient/node');

var dontValidate = ['require', 'getClrType', '_', 's', 'connection', 'user', 'token', 'UI'];
var dontValidateMessages = [];

const languageServerPathDebug = "server/bin/Debug/net5.0/RcmServer.dll";
const languageServerPathRelease = "server/bin/Release/net5.0/RcmServer.dll";
const languageServerDll = "RcmServer.dll";

class JsVirtualDocumentProvider {
	constructor() {
		this._onDidChange = new vscode.EventEmitter();
		this.documents = new Map();
	}

	provideTextDocumentContent(uri) {
		return this.documents.get(uri.toString()) || '';
	}

	updateDocument(uri, content) {
		this.documents.set(uri.toString(), content);
		this._onDidChange.fire(uri);
	}

	get onDidChange() {
		return this._onDidChange.event;
	}
}

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
	// Completion handler for templates hack
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

	// Initialize ESLint
	eslintInstance = new ESLint({
		useEslintrc: false,
		baseConfig: {
			extends: ['eslint:recommended'],
			parserOptions: {
				ecmaVersion: 'latest',
				sourceType: 'module'
			},
			env: {
				browser: false,
				es2021: true
			},
            rules: {
                "no-fallthrough": [ "warn" ],
                "no-constant-condition": [ "warn" ],
                "no-unused-vars": [ "warn" ],
                "no-useless-escape": [ "warn" ]
              }
		}
	});

	// Setup virtual document provider
	virtualProvider = new JsVirtualDocumentProvider();
	context.subscriptions.push(
		vscode.workspace.registerTextDocumentContentProvider('js-in-xml', virtualProvider)
	);

	// ----------------------------
	// 3. Diagnostics (Error Checking)
	// ----------------------------
	const diagnostics = vscode.languages.createDiagnosticCollection('js-in-xml');
	context.subscriptions.push(diagnostics);

    let filenameRegex = /[ \w]+(?=[.])/;

	// Update diagnostics every few seconds
	let lintTimeout;
	vscode.workspace.onDidChangeTextDocument(e => {
        let fileName = filenameRegex.exec(e.document.fileName);
        addFileNameToNotValidateList(fileName);

		clearTimeout(lintTimeout);
		lintTimeout = setTimeout(() => updateDiagnostics(e.document, diagnostics), 1000);
	});

	vscode.workspace.onDidOpenTextDocument(doc => {
        let fileName = filenameRegex.exec(doc.fileName);
        addFileNameToNotValidateList(fileName);

		setTimeout(() => updateDiagnostics(doc, diagnostics), 1000);
	});

	// ----------------------------
	// 4. Completion Provider
	// ----------------------------
	context.subscriptions.push(
		vscode.languages.registerCompletionItemProvider('rcm', {
			async provideCompletionItems(document, position) {
				return getJsCompletions(document, position);
			}
		}, '=', '{') // Trigger characters
	);
}

// ----------------------------
// 5. Diagnostic Utilities
// ----------------------------
async function updateDiagnostics(document, collection) {
	const blocks = findJsBlocks(document.getText());
	const allDiagnostics = [];

	for (const block of blocks) {
		try {
			const results = await eslintInstance.lintText(block.code);
			results[0].messages.forEach(message => {
				if (dontValidateMessages.includes(message.message)) {
					return;
				}
				allDiagnostics.push(createDiagnostic(message, block.startLine));
			});
		} catch (error) {
			console.error('ESLint error:', error);
		}
	}

	collection.set(document.uri, allDiagnostics);
}

function createDiagnostic(message, startLine) {
	return new vscode.Diagnostic(
		new vscode.Range(
			startLine + message.line - 1,
			message.column - 1,
			startLine + (message.endLine || message.line) - 1,
			(message.endColumn || message.column) - 1
		),
		`${message.message} (${message.ruleId || 'syntax'})`,
		message.severity === 2 ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning
	);
}

// ----------------------------
// 6. Completion Logic
// ----------------------------
async function getJsCompletions(document, position) {
	const block = findJsBlockAtPosition(document, position);
	if (!block) return [];

	// Create virtual document URI
	const virtualUri = vscode.Uri.parse(`js-in-xml://${document.uri.path}/block_${block.startLine}.js`);
	virtualProvider.updateDocument(virtualUri, block.code);

	// Calculate position within JS block
	const virtualPosition = new vscode.Position(
		position.line - block.startLine,
		position.character - (position.line === block.startLine ? block.startCol : 0)
	);

	// Get completions from VS Code's JS engine
	try {
		const completions = await vscode.commands.executeCommand(
			'vscode.executeCompletionItemProvider',
			virtualUri,
			virtualPosition
		);
		return completions.items.map(item => ({
			...item,
			range: mapRangeBack(item.range, block)
		}));
	} catch (error) {
		console.error('Completion error:', error);
		return [];
	}
}

// maybe add the attribute js?
const jsCodeBlockPatterns = [
	{
		regex: /<!\[CDATA\[([\s\S]*?)\]\]>/g,
		lineOffset: 1
	}];

// TODO GET THIS SHIT WORKING
const requireImportNamesPattern = /(?<=require.*\(.*)[\w.]+/g;

// ----------------------------
// 7. Block Detection Utilities
// ----------------------------
function findJsBlocks(content) {
	const blocks = [];

	jsCodeBlockPatterns.forEach(({
		regex,
		lineOffset
	}) => {
		let match;
		while ((match = regex.exec(content)) !== null) {
			const linesBefore = content.substring(0, match.index).split('\n').length - 1;
			blocks.push({
				code: match[1].trim(),
				startLine: linesBefore + lineOffset
			});
		}
	});

	return blocks;
}

const jsPositionPattern = [
{
    regex: /<!\[CDATA\[/g,
    end: ']]>',
    colOffset: 9
}];

function findJsBlockAtPosition(document, position) {
	const text = document.getText();

	for (const {
			regex,
			end,
			colOffset
		}
		of jsPositionPattern) {
		let startMatch;
		while ((startMatch = regex.exec(text)) !== null) {
			const startPos = document.positionAt(startMatch.index);
			const endPos = document.positionAt(text.indexOf(end, startMatch.index) + end.length);

			if (position.isAfter(startPos) && position.isBefore(endPos)) {
				return {
					code: text.substring(startMatch.index + colOffset, text.indexOf(end, startMatch.index)),
					startLine: startPos.line,
					startCol: colOffset
				};
			}
		}
	}
	return null;
}

// ----------------------------
// 8. Range Mapping
// ----------------------------
function mapRangeBack(range, block) {
	return new vscode.Range(
		new vscode.Position(
			block.startLine + range.inserting.start.line + 2,
			range.inserting.start.line === 0 ? block.startCol + range.inserting.start.character : range.inserting.start.character
		),
		new vscode.Position(
			block.startLine + range.inserting.end.line + 2,
			range.inserting.end.line === 0 ? block.startCol + range.inserting.end.character : range.inserting.end.character
		)
	);
}

function addFileNameToNotValidateList(filename){
    if(dontValidate.includes(filename[0])){
        return;
    }

    dontValidate.push(filename);
    
    dontValidateMessages = dontValidate.map(element => {
        return `'${element}' is not defined.`;
    });
}

setInterval(() => {
	const activeUris = new Set(
		vscode.window.visibleTextEditors.map(e => e.document.uri.toString())
	);
	virtualProvider.purgeInactive(activeUris);
}, 60_000);

module.exports = {
	activate,
	deactivate: () => client ? client.stop() : undefined
};