using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Threading.Tasks;

namespace RcmServer
{
    public class ChangeHandler : ITextDocumentSyncHandler
    {

        private readonly ILanguageServerFacade _languageServer;


        private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
            new TextDocumentFilter
            {
                Pattern = "**/*.rcm"
            }
        );
        public ChangeHandler(ILanguageServerFacade languageServer)
        {
            _languageServer = languageServer;
        }
        public TextDocumentChangeRegistrationOptions GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            var test = new TextDocumentChangeRegistrationOptions();
            
            return test;
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            var test = new TextDocumentAttributes(uri, "xml");

            return test;
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            // Get the updated text from the document
            var updatedText = request.ContentChanges;
            var documentUri = request.TextDocument.Uri;

            // Example: Validate XML structure (dummy example)
            var diagnostics = ValidateXml(updatedText);

            // Publish diagnostics to the client
            _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = documentUri,
                //Diagnostics = diagnostics
            });

            return Unit.Task;
        }

        private object ValidateXml(Container<TextDocumentContentChangeEvent> updatedText)
        {
            return "Ok";
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        TextDocumentOpenRegistrationOptions IRegistration<TextDocumentOpenRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            throw new System.NotImplementedException();
        }

        TextDocumentCloseRegistrationOptions IRegistration<TextDocumentCloseRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            throw new System.NotImplementedException();
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions, TextSynchronizationCapability>.GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            throw new System.NotImplementedException();
        }
    }
}