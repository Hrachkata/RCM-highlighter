using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RcmServer
{
    public class TextDocumentUtils
    {
        private Cache cache;
        private XmlSchema schema;
        private XmlReaderSettings _validationSettings;
        private XmlReaderSettings _defaultSettings;
        private XmlTextReader schemaTextReader = new XmlTextReader(@"..\..\..\..\rcm.xsd");
        private List<Diagnostic> _diagnostics;
        private List<Diagnostic> diagnostics { get => _diagnostics; set => _diagnostics = value; }

        public TextDocumentUtils( Cache cacheService )
        {
            cache = cacheService;
            diagnostics = new List<Diagnostic>();
            schema = XmlSchema.Read(schemaTextReader, ValidationCallBack);
            // Invalid for now
            //_schemaManager = schemaManager;
            //var schema = await _schemaManager.GetSchemaAsync("https://www.cozyroc.com/sites/default/files/down/schema/rcm-config-1.0.xsd");
        }

        private XmlReaderSettings validationSettings
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
                    _validationSettings.Async = true;
                }

                return _validationSettings;
            }
            set => _validationSettings = value;
        }

        private XmlReaderSettings defaultSettings
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

        public async Task<PublishDiagnosticsParams> ValidateBySchemaAsync(string documentContent, DocumentUri documentUri)
        {
            cache.UpdateDocument(documentContent);

            var resourceNames = new HashSet<string>();
            var fieldNames = new HashSet<string>();

            cache.ScriptLine = int.MaxValue;

            cache.ClearTemplateFieldCache();
            cache.ClearTemplateResourceCache();

            try
            {
                bool doneWithTemplates = false;

                using (StringReader stringReader = new StringReader(documentContent))
                using (XmlReader XMLdocReader = XmlReader.Create(stringReader, validationSettings))
                {
                    // Validate the entire xml file
                    while (await XMLdocReader.ReadAsync())
                    {
                        // Get the script position, we do not want autocompletes inside the JS
                        if (XMLdocReader.NodeType == XmlNodeType.Element && XMLdocReader.Name == "Script")
                        {
                            cache.ScriptLine = (XMLdocReader is IXmlLineInfo xmlLine && xmlLine.HasLineInfo()) ? xmlLine.LineNumber : -1;
                        }

                        if (XMLdocReader.NodeType == XmlNodeType.Element && !doneWithTemplates )
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
                                    // When all templates have been cached flag it
                                    doneWithTemplates = true;
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
                    };
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
                // Log something?
                diagnostics.Add(currentDiagItem);
            }
            catch (Exception er)
            {
                // Log something?
                Console.WriteLine(er);
            }
            finally
            {
                cache.UpdateTemplateFieldCache(fieldNames);
                cache.UpdateTemplateResourceCache(resourceNames);
            }

            var publishDiagnosticsParams = new PublishDiagnosticsParams
            {
                Uri = documentUri,
                Diagnostics = diagnostics
            };

            return publishDiagnosticsParams;
        }

        public void ValidationCallBack(object? sender, ValidationEventArgs args)
        {
            var changedText = sender?.GetType().GetProperty("Name")?.GetValue(sender, null).ToString();

            if (changedText == null)
            {
                return;
            }

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

        public void ClearDiagnostics()
        {
            diagnostics.Clear();
        }
    }
}