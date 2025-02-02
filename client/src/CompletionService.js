const vscode = require('vscode');

async function getJsCompletions(document, position) {
    const block = findJsBlockAtPosition(document, position);
    if (!block) return [];

    // Create isolated virtual document for this block
    const virtualUri = vscode.Uri.parse(
        `js-in-xml://${document.uri.path}/block_${block.startLine}.js?ts=${Date.now()}`
    );
    
    // Use only the current block's code
    virtualProvider.updateDocument(virtualUri, block.code);

    // Calculate precise virtual position
    const virtualPosition = new vscode.Position(
        position.line - block.startLine,
        position.character - (position.line === block.startLine ? block.startCol : 0)
    );

    try {
        // Get raw completions from VS Code's JS engine
        const completions = await vscode.commands.executeCommand(
            'vscode.executeCompletionItemProvider',
            virtualUri,
            virtualPosition
        );

        // Map completion ranges back to original document
        return completions.items.map(item => ({
            ...item,
            range: item.range ? mapRangeToOriginal(item.range, block) : undefined
        }));
    } catch (error) {
        console.error('Completion error:', error);
        return [];
    }
}

// Helper function to map virtual positions to original document
function mapRangeToOriginal(virtualRange, block) {
    return new vscode.Range(
        block.startLine + virtualRange.inserting.start.line,
        virtualRange.inserting.start.character + (
            virtualRange.inserting.start.line === 0 ? block.startCol : 0
        ),
        block.startLine + virtualRange.inserting.end.line,
        virtualRange.inserting.end.character + (
            virtualRange.inserting.end.line === 0 ? block.startCol : 0
        )
    );
}

// Simplified block detection
function findJsBlockAtPosition(document, position) {
    const text = document.getText();
    const offset = document.offsetAt(position);
    
    // Find CDATA blocks
    const cdataBlocks = [...text.matchAll(/<!\s*\[CDATA\[([\s\S]*?)\]\]>/g)];
    for (const match of cdataBlocks) {
        const start = match.index + match[0].indexOf('[CDATA[') + 7;
        const end = match.index + match[0].indexOf(']]>');
        if (offset >= start && offset <= end) {
            return {
                code: match[1],
                startLine: document.positionAt(start).line,
                startCol: document.positionAt(start).character,
                startOffset: start,
                endOffset: end
            };
        }
    }

    return null;
}

module.exports = { getJsCompletions };