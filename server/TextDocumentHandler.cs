using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;


#pragma warning disable CS0618

namespace RcmServer
{
    public class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        private readonly ILanguageServer _languageServer;
        private TextDocumentUtils utils;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
            new TextDocumentFilter
            {
                Pattern = "**/*.rcm"
            }
        );

        public TextDocumentHandler( ILanguageServer languageServer, TextDocumentUtils TextDocumentUtils, ILanguageServerConfiguration configuration)
        {
            _languageServer = languageServer;
            utils = TextDocumentUtils;
            _configuration = configuration;
        }



        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;



        public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();

            TextDocumentContentChangeEvent changedEvent = null;

            foreach (TextDocumentContentChangeEvent? item in notification.ContentChanges)
            {
                changedEvent = item;
            }

            var diagnosticArr = await utils.ValidateBySchemaAsync(changedEvent.Text, notification.TextDocument.Uri);

            _languageServer.TextDocument.PublishDiagnostics(diagnosticArr);

            utils.ClearDiagnostics();

            return Unit.Value;
        }



        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();

            var diagnosticArr = await utils.ValidateBySchemaAsync(notification.TextDocument.Text, notification.TextDocument.Uri);

            _languageServer.TextDocument.PublishDiagnostics(diagnosticArr);

            return Unit.Value;
        }



        public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
            {
                disposable.Dispose();
            }

            return Unit.Task;
        }



        public override async Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Value;
        }



        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _textDocumentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };



        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, "csharp");



        //internal class MyDocumentSymbolHandler : IDocumentSymbolHandler
        //{
        //    public async Task<SymbolInformationOrDocumentSymbolContainer> Handle(
        //        DocumentSymbolParams request,
        //        CancellationToken cancellationToken
        //    )
        //    {
        //        // you would normally get this from a common source that is managed by current open editor, current active editor, etc.
        //        var content = await File.ReadAllTextAsync(DocumentUri.GetFileSystemPath(request), cancellationToken).ConfigureAwait(false);
        //        var lines = content.Split('\n');
        //        var symbols = new List<SymbolInformationOrDocumentSymbol>();
        //        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        //        {
        //            var line = lines[lineIndex];
        //            var parts = line.Split(' ', '.', '(', ')', '{', '}', '[', ']', ';');
        //            var currentCharacter = 0;
        //            foreach (var part in parts)
        //            {
        //                if (string.IsNullOrWhiteSpace(part))
        //                {
        //                    currentCharacter += part.Length + 1;
        //                    continue;
        //                }

        //                symbols.Add(
        //                    new DocumentSymbol
        //                    {
        //                        Detail = part,
        //                        Deprecated = true,
        //                        Kind = SymbolKind.Field,
        //                        Tags = new[] { SymbolTag.Deprecated },
        //                        Range = new Range(
        //                            new Position(lineIndex, currentCharacter),
        //                            new Position(lineIndex, currentCharacter + part.Length)
        //                        ),
        //                        SelectionRange =
        //                            new Range(
        //                                new Position(lineIndex, currentCharacter),
        //                                new Position(lineIndex, currentCharacter + part.Length)
        //                            ),
        //                        Name = part
        //                    }
        //                );
        //                currentCharacter += part.Length + 1;
        //            }
        //        }

        //        await Task.Delay(2000, cancellationToken);
        //        return symbols;
        //    }

        //    public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) => new DocumentSymbolRegistrationOptions
        //    {
        //        DocumentSelector = TextDocumentSelector.ForLanguage("csharp")
        //    };
        //}

        /*internal class MyWorkspaceSymbolsHandler : IWorkspaceSymbolsHandler
        {
            private readonly IServerWorkDoneManager _serverWorkDoneManager;
            private readonly IProgressManager _progressManager;
            private readonly ILogger<MyWorkspaceSymbolsHandler> _logger;

            public MyWorkspaceSymbolsHandler(IServerWorkDoneManager serverWorkDoneManager, IProgressManager progressManager, ILogger<MyWorkspaceSymbolsHandler> logger)
            {
                _serverWorkDoneManager = serverWorkDoneManager;
                _progressManager = progressManager;
                _logger = logger;
            }

            public async Task<Container<WorkspaceSymbol>> Handle(
                WorkspaceSymbolParams request,
                CancellationToken cancellationToken
            )
            {
                using var reporter = _serverWorkDoneManager.For(
                    request, new WorkDoneProgressBegin
                    {
                        Cancellable = true,
                        Message = "This might take a while...",
                        Title = "Some long task....",
                        Percentage = 0
                    }
                );
                using var partialResults = _progressManager.For(request, cancellationToken);
                if (partialResults != null)
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 20
                        }
                    );
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 40
                        }
                    );
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 50
                        }
                    );
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    partialResults.OnNext(
                        new[] {
                        new WorkspaceSymbol {
                            ContainerName = "Partial Container",
                            Kind = SymbolKind.Constant,
                            Location = new Location {
                                Range = new Range(
                                    new Position(2, 1),
                                    new Position(2, 10)
                                )
                            },
                            Name = "Partial name"
                        }
                        }
                    );

                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 70
                        }
                    );
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 90
                        }
                    );

                    partialResults.OnCompleted();
                    return new WorkspaceSymbol[] { };
                }

                try
                {
                    return new[] {
                    new WorkspaceSymbol {
                        ContainerName = "Container",
                        Kind = SymbolKind.Constant,
                        Location = new Location {
                            Range = new Range(
                                new Position(1, 1),
                                new Position(1, 10)
                            )
                        },
                        Name = "name"
                    }
                };
                }
                finally
                {
                    reporter.OnNext(
                        new WorkDoneProgressReport
                        {
                            Cancellable = true,
                            Percentage = 100
                        }
                    );
                }
            }

            public WorkspaceSymbolRegistrationOptions GetRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities) => new WorkspaceSymbolRegistrationOptions();
        }*/
    }
}