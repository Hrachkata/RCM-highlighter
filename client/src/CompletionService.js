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

const jsWordPattern = /(-?\d*\.\d\w*)|([^\`\~\!\@\#\%\^\&\*\(\)\=\+\[\{\]\}\\\|\;\:\'\"\,\.\<\>\/\?\s]+)/g;

async function getJsCompletions(document, position) {
    const block = findJsBlockAtPosition(document, position);
    if (!block) return [];

    const virtualUri = vscode.Uri.parse(
        `js-in-xml://${document.uri.path}/block_${block.startLine}.js`
    );
    
    const virtualPosition = new vscode.Position(
        position.line - block.startLine,
        position.character - (position.line === block.startLine ? block.startCol : 0)
    );

    const cached = getCachedVirtualUri(virtualUri, block);

    try {
        // Get all regex matches
        const completions = [...block.code.matchAll(jsWordPattern)];
        
        // Extract words, filter by length, and deduplicate
        const words = completions.map(match => match[0])
                                 .filter(word => word && word.length > 2);
        const uniqueWords = [...new Set(words)]; // Using Set for uniqueness

        const result = new vscode.CompletionList();

        // Add each unique word to completions
        uniqueWords.forEach(word => {
            result.items.push({
                label: word,
                // Note: Original range handling was incorrect (match doesn't include ranges)
                // Consider calculating range based on match index if needed
                range: undefined 
            });
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