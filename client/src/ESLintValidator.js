const vscode = require('vscode');
const { ESLint } = require('eslint');

const dontValidate = ['require', 'getClrType', '_', 's', 'connection', 'user', 'token', 'UI', 'Uri', 'Base64', 'OAuth2', 'alert', 'doT', 'RestConnection', 
					'utils', 'OAuth1', 'b64_hmac_sha1', 'json2xml', 'aws4s', 'SOAP', 'b64_hmac_sha256', 'dateFormat', 'hex_hmac_sha256', 'hex_hmac_sha1',
					'Google', 'xml2json']
const diagnosticSource = 'ESLint-integrated-srv'

eslintInstance = new ESLint(getESLintConfig());

async function updateDiagnostics(document, collection) {
	const block = findJsBlocks(document.getText());

	if (!block) {
		return;
	}

	const allDiagnostics = [];

	try {
		const results = await eslintInstance.lintText(block.cleanedJs);
		results[0].messages.forEach(message => {
			allDiagnostics.push(createDiagnostic(message, block.startLine));
		});
	} catch (error) {
		console.error('ESLint error:', error);
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
const jsCodePatterndirty =  /<!\[CDATA\[(\s)*?([\s\S]*)\]\]>/gd;
const elementsInsideDirtyCodePattern = /]]>[\s\S]*?<!\[CDATA\[.*\n?/gd;
const newlineRegex =  /\n/g
const newline =  '\n';

function findJsBlocks(content) {
	let JSdirty = jsCodePatterndirty.exec(content);
	if (!JSdirty) {
		return;	
	} 

	let allJsCodeDirty = JSdirty[2];

	var match = elementsInsideDirtyCodePattern.exec(allJsCodeDirty);

	while( match ){
		let replacementNewlineCount = match[0].match(newlineRegex)?.length || 0;
		let replacement = newline.repeat(replacementNewlineCount);
		allJsCodeDirty = allJsCodeDirty.replace(match[0], replacement);
		match = elementsInsideDirtyCodePattern.exec(allJsCodeDirty);
	}

	const linesBefore = content.substring(0, JSdirty.index).split('\n').length;

	let resultObj = {
		cleanedJs: allJsCodeDirty,
		startLine: linesBefore - 1
	}

	return resultObj;
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

module.exports = { updateDiagnostics, dontValidate, refreshEslintConfig };