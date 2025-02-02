const vscode = require('vscode');

// ----------------------------
// 6. Completion Logic
// ----------------------------
async function getJsCompletions(document, position, virtualProvider) {
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
		var result = completions.items.map(item => ({
			...item,
			range: mapRangeBack(item.range, block)
		}));

		return result;
	} catch (error) {
		console.error('Completion error:', error);
		return [];
	}
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

module.exports = { getJsCompletions };