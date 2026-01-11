const vscode = require('vscode');

// Doesn't work well for autocompletion

class JsVirtualDocumentProvider {
	provideTextDocumentContent(uri) {
		const params = new URLSearchParams(uri.query);
		const docUri = vscode.Uri.parse(params.get('doc'));
		const jsBlockOffsetStart = Number(params.get('jsBlockOffsetStart'));
		const jsBlockOffsetEnd = Number(params.get('jsBlockOffsetEnd'));

		const xmlDoc = vscode.workspace.textDocuments.find(
			d => d.uri.toString() === docUri.toString()
		);

		if (!xmlDoc) return '}';

		const jsBlock = xmlDoc.getText().slice(jsBlockOffsetStart, jsBlockOffsetEnd);

		if (!jsBlock) return '}';

    	return jsBlock;
	}
}

module.exports = {
	JsVirtualDocumentProvider
};