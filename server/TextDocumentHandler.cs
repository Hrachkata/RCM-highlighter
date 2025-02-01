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
        private readonly Serilog.ILogger _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private Cache cache;
        private readonly ILanguageServer _languageServer;
        // private readonly SchemaManager _schemaManager;
        private List<Diagnostic> _diagnostics;
        private XmlTextReader schemaTextReader = new XmlTextReader(@"..\..\..\..\rcm.xsd");
        private XmlSchema schema;
        private XmlReaderSettings _validationSettings;
        private XmlReaderSettings _defaultSettings;

        private readonly TextDocumentSelector _textDocumentSelector = new TextDocumentSelector(
            new TextDocumentFilter
            {
                Pattern = "**/*.rcm"
            }
        );

        private List<Diagnostic> diagnostics { get => _diagnostics; set => _diagnostics = value; }

        public XmlReaderSettings validationSettings
        {
            get
            {
                if (_validationSettings == null)
                {
                    _validationSettings = new XmlReaderSettings();
                    _validationSettings.ValidationType = ValidationType.Schema;
                    _validationSettings.Schemas.Add(schema);
                    _validationSettings.ValidationEventHandler += new ValidationEventHandler(ValidationCallBack);
                    _validationSettings.IgnoreComments = true;
                }

                return _validationSettings;
            } 
            set => _validationSettings = value; 
        }

        public XmlReaderSettings defaultSettings
        {
            get
            {
                if (_defaultSettings == null)
                {
                    _defaultSettings = new XmlReaderSettings();
                    _defaultSettings.IgnoreComments = true;
                    _defaultSettings.Async = true;
                }

                return _defaultSettings;
            }
            set => _defaultSettings = value;
        }

        public TextDocumentHandler(ILanguageServer languageServer, Serilog.ILogger logger, ILanguageServerConfiguration configuration, SchemaManager schemaManager, Cache cacheService)
        {
            _languageServer = languageServer;
            _logger = logger;
            _configuration = configuration;
            cache = cacheService;
            diagnostics = new List<Diagnostic>();
            schema = XmlSchema.Read(schemaTextReader, ValidationCallBack);
            // Invalid for now
            //_schemaManager = schemaManager;
            //var schema = await _schemaManager.GetSchemaAsync("https://www.cozyroc.com/sites/default/files/down/schema/rcm-config-1.0.xsd");
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();

            _logger.Information("Changed text document.");

            TextDocumentContentChangeEvent changedEvent = null;

            foreach (TextDocumentContentChangeEvent? item in notification.ContentChanges)
            {
                changedEvent = item;
            }

            var diagnosticArr = ValidateBySchema(changedEvent.Text, notification.TextDocument.Uri);

            _languageServer.TextDocument.PublishDiagnostics(diagnosticArr);

            diagnostics.Clear();

            return Unit.Value;
        }

        public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();

            _logger.Information("Opened document.");

            var diagnosticArr = ValidateBySchema(notification.TextDocument.Text, notification.TextDocument.Uri);

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

        public override async Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) {

            var resourceNames = new HashSet<string>();
            var fieldNames = new HashSet<string>();

            using (StringReader stringReader = new StringReader(notification.Text))
            using (XmlReader XMLdocReader = XmlReader.Create(stringReader, defaultSettings))
            {
                while (await XMLdocReader.ReadAsync()) {
                    if (XMLdocReader.NodeType == XmlNodeType.Element)
                    {
                        bool insideTargetParent = false;
                        string targetParent = "Template";
                        string targetAttribute = "Name"; 

                        while (await XMLdocReader.ReadAsync())
                        {
                            if (XMLdocReader.NodeType == XmlNodeType.Element && XMLdocReader.Name == targetParent)
                            {
                                insideTargetParent = true;
                            }
                            else if (XMLdocReader.NodeType == XmlNodeType.EndElement && XMLdocReader.Name == targetParent)
                            {
                                break;
                            }

                            if (insideTargetParent && XMLdocReader.NodeType == XmlNodeType.Element && XMLdocReader.HasAttributes)
                            {
                                string elementName = XMLdocReader.Name;

                                string? attributeValue = XMLdocReader.GetAttribute(targetAttribute);

                                if (attributeValue == null)
                                {
                                    continue;
                                }

                                switch (elementName)
                                {
                                    case "Resource":
                                        resourceNames.Add(attributeValue);
                                        break;
                                    case "Field":
                                        fieldNames.Add(attributeValue);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            cache.ClearTemplateFieldCache();
            cache.ClearTemplateResourceCache();
            cache.UpdateTemplateFieldCache(fieldNames);
            cache.UpdateTemplateResourceCache(resourceNames);

            return Unit.Value; 
        }

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

        private PublishDiagnosticsParams ValidateBySchema(string documentContent, DocumentUri documentUri)
        {
            XmlReader reader;

            try
            {
                using (StringReader stringReader = new StringReader(documentContent))
                using (XmlReader validator = XmlReader.Create(stringReader, validationSettings))
                {
                    // Validate the entire xml file
                    while (validator.Read()) ;
                }
            }
            catch (XmlException e)
            {
                var range = new Range(
                    new Position(e.LineNumber - 1, e.LinePosition - 1),
                    new Position(e.LineNumber - 1, e.LinePosition + 999));

                var currentDiagItem = new Diagnostic()
                {
                    Severity = DiagnosticSeverity.Error,
                    Code = "banana",
                    Message = e.Message,
                    Source = "RCM-NET-server",
                    Range = range
                };

                diagnostics.Add(currentDiagItem);
            }

            var publishDiagnosticsParams = new PublishDiagnosticsParams
            {
                Uri = documentUri,
                Diagnostics = diagnostics
            };

            return publishDiagnosticsParams;
        }

        private void ValidationCallBack(object? sender, ValidationEventArgs args)
        {
            var changedText = sender?.GetType().GetProperty("Name")?.GetValue(sender, null).ToString();

            var range = new Range(
                    new Position(args.Exception.LineNumber - 1, args.Exception.LinePosition - 1),
                    new Position(args.Exception.LineNumber - 1, args.Exception.LinePosition + changedText.Length - 1));


            var currentDiagItem = new Diagnostic()
            {
                Severity = args.Severity == XmlSeverityType.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                Code = "banana",
                Message = args.Message,
                Source = "RCM-NET-server",
                Range = range
            };

            diagnostics.Add(currentDiagItem);
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

            await Task.Delay(2000, cancellationToken);
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