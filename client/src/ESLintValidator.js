const vscode = require('vscode');
const {
	ESLint
} = require('eslint');

var dontValidate = ['require', 'getClrType', '_', 's', 'connection', 'user', 'token', 'UI', 'Uri', 'Base64'];
const diagnosticSource = 'ESLint-integrated-srv'

class JsVirtualDocumentProvider {
    constructor() {
        this._onDidChange = new vscode.EventEmitter();
        this.documents = new Map();
        this.lastAccessed = new Map(); // Track last access time
    }

    provideTextDocumentContent(uri) {
        const uriString = uri.toString();
        if (this.documents.has(uriString)) {
            this.lastAccessed.set(uriString, Date.now());
            return this.documents.get(uriString);
        }
        return '';
    }

    updateDocument(uri, content) {
        const uriString = uri.toString();
        this.documents.set(uri.toString(), content);
        this.lastAccessed.set(uriString, Date.now());
        this._onDidChange.fire(uri);
    }

    purgeInactive(activeVirtualUris, maxAgeMinutes = 15) {
        const now = Date.now();
        const maxAge = maxAgeMinutes * 60 * 1000;
        
        for (const [uriString] of this.documents) {
            const lastAccess = this.lastAccessed.get(uriString) || 0;
            const isActive = activeVirtualUris.has(uriString);
            const isExpired = (now - lastAccess) > maxAge;

            if (!isActive && isExpired) {
                this.documents.delete(uriString);
                this.lastAccessed.delete(uriString);
            }
        }
    }

    get onDidChange() {
        return this._onDidChange.event;
    }
}

const filenameRegex = /[ \w]+(?=[.])/;
let fileName = filenameRegex.exec(vscode.window.activeTextEditor.document.fileName)[0];
dontValidate.push(fileName);

// Initialize ESLint
eslintInstance = new ESLint(getESLintConfig());

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
				allDiagnostics.push(createDiagnostic(message, block.startLine));
			});
		} catch (error) {
			console.error('ESLint error:', error);
		}
	}

	collection.set(document.uri, allDiagnostics);
}

function createDiagnostic(message, startLine) {
	var diagnosticObj =  new vscode.Diagnostic(
		new vscode.Range(
			startLine + message.line - 1,
			message.column - 1,
			startLine + (message.endLine || message.line) - 1,
			(message.endColumn || message.column) - 1
		),
		`${message.message} (${message.ruleId || 'syntax'})`,
		message.severity === 2 ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning
	);

	diagnosticObj.code = 'OnCol' + message.column + 'OnLine' + message.line;
	diagnosticObj.source = diagnosticSource;
	return diagnosticObj;
}



// maybe add the attribute js?
const jsCodeBlockPatterns = [
	{
		regex: /<!\[CDATA\[([\s\S]*?)\]\]>/g,
		lineOffset: 1
	}];

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

function refreshEslintConfig(){
	eslintInstance = new ESLint(getESLintConfig());
}

function generateGlobalsObject(globalVars){
	let result = {};
	
	globalVars.forEach(element => {
		result[element] = 'readonly';
	});

	return result;
}

function getESLintConfig() {
	return {
		useEslintrc: false,
		overrideConfig: {
			extends: ['eslint:recommended'],
			parserOptions: {
				ecmaVersion: 'latest',
				sourceType: 'script' // Changed from 'module' for CommonJS-like env
			},
			env: {
				browser: false,
				es2021: true,
				node: true // Enable Node.js globals if needed
			},
			globals: generateGlobalsObject(dontValidate),
			rules: {
				"no-fallthrough": ["warn"],
				"no-constant-condition": ["warn", { "checkLoops": false }],
				"no-unused-vars": ["warn"],
				"no-useless-escape": "off" // Consider disabling if using XML entities
			}
		},
		//resolvePluginsRelativeTo: 'D:\\SSIS+\\install\\files\\JS'
	};
}

module.exports = { JsVirtualDocumentProvider, updateDiagnostics, dontValidate, refreshEslintConfig };