using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
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

        public CompletionHandler(Cache cache)
        {
            _cache = cache;
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var items = new[]
            {
                new CompletionItem
                {
                    Label = "Duck",
                    Kind = CompletionItemKind.Snippet,
                    Detail = "🦆.",
                    InsertText = "🦆🦆🦆🦆🦆",
                    Documentation = "🦆."
                },

                new CompletionItem
                {
                    Label = "\"fDuck\"",
                    Kind = CompletionItemKind.Snippet,
                    Detail = "🦆.",
                    InsertText = "🦆🦆🦆🦆🦆",
                    Documentation = "🦆."
                }
            };

            // check if the typed data has a possibility of being in a template attribute
            if (request.Position.Character < 15)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var uri = request.TextDocument.Uri.Path;
            var currentLine = ReadCurrentLine(uri.Substring(1, uri.Length - 1), request.Position.Line + 1);

            // <Component Name="action" Template="ShortText" />
            // matches
            // Template="S
            var templateLiteralRegex = new Regex(@"(Template.*=.*?"")(.)");
            var position = templateLiteralRegex.Match(currentLine).Groups[1].Index;

            if (position == request.Position.Character)
            {
                var fieldRegex = new Regex("Component|Field");
                var fieldMatch = fieldRegex.Match(currentLine);

                var resourceRegex = new Regex("Resource");
                var resourceMatch = resourceRegex.Match(currentLine);


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