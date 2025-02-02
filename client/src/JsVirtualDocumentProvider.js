const vscode = require('vscode');

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

module.exports = { JsVirtualDocumentProvider };