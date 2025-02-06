using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RcmServer
{
    public class CompletionHandler : IJsonRpcRequestHandler<CompletionParams, CompletionList>
    {
        private Cache _cache;

        private List<CompletionItem> _completionsCache = new List<CompletionItem>{
                new CompletionItem
                {
                    Label = "Duck",
                    Kind = CompletionItemKind.Snippet,
                    Detail = "🦆",
                    InsertText = "🦆",
                    Documentation = "🦆."
                },
                new CompletionItem
                {
                    Label = "Documentation",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = "<Documentation>$1</Documentation>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Field",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Field Name=""$1"" Template=""$2""/>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "FField",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Field Name=""$1"">
    <Component Name=""$2"" Template=""$3""/>
</Field>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Component",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Component Name=""$1"" Template=""$2""/>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "UserParams",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"					<User>
						<Parameter Name=""$1"">
							<Documentation>Required. ""$2""</Documentation>
						</Parameter>
					</User>$0",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "UserParam",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"						<Parameter Name=""$1"">
							<Documentation>Required. ""$2""</Documentation>
						</Parameter>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Params",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"					<Parameters>
						<Parameter Name=""application/json"" Value=""{{=$1}}"" Type=""Body"" />
                    </Parameters>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Param",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"<Parameter Name=""application/json"" Value=""{{=$1}}"" Type=""Body"" />",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Name",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Name=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Value",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Value""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Type",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Type""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Default",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Default""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Iterator",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"					<Iterator>
						<Next Value=""$1""/>
					</Iterator>",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Resource",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Read",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Read",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Create",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Update",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Update",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Delete",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Resource",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Enumerable",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Enumerable=""true""",
                    InsertTextFormat = InsertTextFormat.Snippet
                },
                new CompletionItem
                {
                    Label = "Template",
                    Kind = CompletionItemKind.Snippet,
                    InsertText = @"Template=""$1""",
                    InsertTextFormat = InsertTextFormat.Snippet
                }
            };

        public CompletionHandler(Cache cache)
        {
            _cache = cache;
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var items = new List<CompletionItem>(_completionsCache);

            // check if the typed data has a possibility of being in a template attribute
            if (request.Position.Character < 15)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var uri = request.TextDocument.Uri.Path;
            var currentLine = ReadCurrentLine(uri.Substring(1, uri.Length - 1), request.Position.Line + 1);
            currentLine = currentLine.Replace("\"", "-");
            // <Component Name="action" Template="ShortText" />
            // matches
            // Template="S
            var templateLiteralRegex = new Regex(@"(Template.*?=.*?)(.)");
            var position = templateLiteralRegex.Match(currentLine).Groups[2].Index + 2;

            if (position != request.Position.Character)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var fieldRegex = new Regex("Component|Field|Fields");
            var fieldMatch = fieldRegex.Match(currentLine);

            if (fieldMatch.Success)
            {
                items.Clear();

                foreach (var item in _cache.GetFields())
                {
                    items.Add(new CompletionItem { 
                        Label = item,
                        Kind = CompletionItemKind.Reference,
                        InsertText = item
                    });
                }

                return Task.FromResult(new CompletionList(items));
            }

            var resourceRegex = new Regex("Resource");
            var resourceMatch = resourceRegex.Match(currentLine);

            if (resourceMatch.Success)
            {
                foreach (var item in _cache.GetResources())
                {
                    items.Add(new CompletionItem
                    {
                        Label = item,
                        Kind = CompletionItemKind.Reference,
                        InsertText = item
                    });
                }

                return Task.FromResult(new CompletionList(items));
            }

            return Task.FromResult(new CompletionList(items));
        }

        static string ReadCurrentLine(string filePath, int lineNumber)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                for (int i = 1; i <= lineNumber; i++)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                        return null; 

                    if (i == lineNumber)
                        return line;
                }
            }

            return null;
        }
    }
}