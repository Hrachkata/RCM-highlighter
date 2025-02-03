const vscode = require('vscode');

const virtualDocCache = new Map();
var completionCache = new vscode.CompletionList();

async function getCachedVirtualUri(virtualUri, block){
    const cached = virtualDocCache.get(virtualUri.toString());
    const needsUpdate = !cached || cached.version !== block.version;

    if (needsUpdate) {
        // Update content only when changed
        virtualProvider.updateDocument(virtualUri, block.code);
        virtualDocCache.set(virtualUri.toString(), {
            version: block.version,
            doc: await vscode.workspace.openTextDocument(virtualUri)
        });
    }
}

async function getJsCompletions(document, position) {
    const block = findJsBlockAtPosition(document, position);
    if (!block) return [];

    // Create isolated virtual document for this block
    const virtualUri = vscode.Uri.parse(
        `js-in-xml://${document.uri.path}/block_${block.startLine}.js`
    );
    
    // Calculate precise virtual position
    const virtualPosition = new vscode.Position(
        position.line - block.startLine,
        position.character - (position.line === block.startLine ? block.startCol : 0)
    );

    const cached = getCachedVirtualUri(virtualUri, block);

    try {
        // Get raw completions from VS Code's JS engine
        let completions = await vscode.commands.executeCommand(
            'vscode.executeCompletionItemProvider',
            virtualUri,
            virtualPosition,
            undefined,
            20
        );

        let result = new vscode.CompletionList();

        // Map completion ranges back to original document
        completions.items.flatMap(item => {
                if (!item || item?.kind != 5) {
                    result.items.push({
                        ...item,
                        range: item.range ? mapRangeToOriginal(item.range, block) : undefined
                    });
                };
            });

        completionCache = result;
        return result;

    } catch (error) {
        console.error('Completion error:', error);
        return [];
    }
}

async function getPropertyAccessCompletions(document, position) {
    // Move position back by 1 to capture the '.' character

    try {
        if (completionCache) { 
            let currentRange = new vscode.Range(
                position.with(undefined, position.character),
                position
            )
    
            return completionCache.items.map(item => ({
                ...item,
                range: currentRange
            }));
        }
    } catch (error) {
        console.error('Property completion error:', error);
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

module.exports = { getJsCompletions, getPropertyAccessCompletions };