using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RcmServer
{
    public class CompletionHandler : IJsonRpcRequestHandler<CompletionParams, CompletionList>
    {
        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            // Example completion items
            var items = new[]
            {
            new CompletionItem
            {
                Label = "Resource",
                Kind = CompletionItemKind.Text,
                Detail = "An example XML tag",
                InsertText = "<ExampleTag></ExampleTag>",
                Documentation = "This is an example tag."
            },
            new CompletionItem
            {
                Label = "Name",
                Kind = CompletionItemKind.Snippet,
                Detail = "Name attribute.",
                InsertText = "Name=\"\"",
                Documentation = "Name attribute."
            },
            new CompletionItem
            {
                Label = "Duck",
                Kind = CompletionItemKind.Snippet,
                Detail = "🦆.",
                InsertText = "🦆🦆🦆🦆🦆",
                Documentation = "🦆."
            }
        };

            return Task.FromResult(new CompletionList(items));
        }
    }
}