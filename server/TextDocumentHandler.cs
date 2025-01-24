using System;
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
        private readonly Serilog.ILogger _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServerFacade _languageServer;
        private readonly SchemaManager _schemaManager;

        private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
            new TextDocumentFilter
            {
                Pattern = "**/*.rcm"
            }
        );

        public TextDocumentHandler(ILanguageServerFacade languageServer, Serilog.ILogger logger, ILanguageServerConfiguration configuration, SchemaManager schemaManager)
        {
            _languageServer = languageServer;
            _logger = logger;
            _configuration = configuration;
            _schemaManager = schemaManager;
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            _logger.Information("Changed text document.");

            //var schema = await _schemaManager.GetSchemaAsync("https://www.cozyroc.com/sites/default/files/down/schema/rcm-config-1.0.xsd");

            XmlTextReader reader1 = new XmlTextReader(@"C:\Users\Tigan\Downloads\rcm.xsd");
            XmlSchema schema = XmlSchema.Read(reader1, ValidationCallBack);

            // Set the validation settings.
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.Schemas.Add(schema);
            settings.ValidationEventHandler += new ValidationEventHandler(ValidationCallBack);

            TextDocumentContentChangeEvent test = null;

            foreach (TextDocumentContentChangeEvent? item in notification.ContentChanges)
            {
                test = item;
            }

            XmlReader reader;
            // Create the XmlReader object.
            try
            {
                using (StringReader stringReader = new StringReader(test.Text))
                using (XmlReader validator = XmlReader.Create(stringReader, settings))
                {
                    // Validate the entire xml file
                    while (validator.Read()) ;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }

            return Unit.Value;
        }

        // Display any warnings or errors.
        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                Console.WriteLine("\tWarning: Matching schema not found.  No validation occurred." + args.Message);
            else
                Console.WriteLine("\tValidation error: " + args.Message);

        }

        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();
            _logger.Information("Hello world!");
            await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);


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

        public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _textDocumentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, "csharp");

        public static bool ValidateXml(string xmlString, XmlSchema schema)
        {
            try
            {
                // Load the schema into a schema set
                var schemaSet = new XmlSchemaSet();
                schemaSet.Add(schema);

                // Configure validation settings
                var settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    Schemas = schemaSet
                };

                // Validate XML
                using (var xmlReader = XmlReader.Create(new StringReader(xmlString), settings))
                {
                    while (xmlReader.Read()) { } // Process the entire XML document
                }

                return true; // Valid XML
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"XML validation failed: {ex.Message}", ex);
            }
        }
    }

    internal class MyDocumentSymbolHandler : IDocumentSymbolHandler
    {
        public async Task<SymbolInformationOrDocumentSymbolContainer> Handle(
            DocumentSymbolParams request,
            CancellationToken cancellationToken
        )
        {
            // you would normally get this from a common source that is managed by current open editor, current active editor, etc.
            var content = await File.ReadAllTextAsync(DocumentUri.GetFileSystemPath(request), cancellationToken).ConfigureAwait(false);
            var lines = content.Split('\n');
            var symbols = new List<SymbolInformationOrDocumentSymbol>();
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var parts = line.Split(' ', '.', '(', ')', '{', '}', '[', ']', ';');
                var currentCharacter = 0;
                foreach (var part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part))
                    {
                        currentCharacter += part.Length + 1;
                        continue;
                    }

                    symbols.Add(
                        new DocumentSymbol
                        {
                            Detail = part,
                            Deprecated = true,
                            Kind = SymbolKind.Field,
                            Tags = new[] { SymbolTag.Deprecated },
                            Range = new Range(
                                new Position(lineIndex, currentCharacter),
                                new Position(lineIndex, currentCharacter + part.Length)
                            ),
                            SelectionRange =
                                new Range(
                                    new Position(lineIndex, currentCharacter),
                                    new Position(lineIndex, currentCharacter + part.Length)
                                ),
                            Name = part
                        }
                    );
                    currentCharacter += part.Length + 1;
                }
            }

            // await Task.Delay(2000, cancellationToken);
            return symbols;
        }

        public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) => new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("csharp")
        };
    }

    internal class MyWorkspaceSymbolsHandler : IWorkspaceSymbolsHandler
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
    }
}