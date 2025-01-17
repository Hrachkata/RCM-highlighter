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
                Label = "ExampleTag",
                Kind = CompletionItemKind.Text,
                Detail = "An example XML tag",
                InsertText = "<ExampleTag></ExampleTag>",
                Documentation = "This is an example tag."
            },
            new CompletionItem
            {
                Label = "AnotherTag",
                Kind = CompletionItemKind.Snippet,
                Detail = "Another XML tag",
                InsertText = "<AnotherTag>${1}</AnotherTag>",
                Documentation = "This is another example tag."
            }
        };

            return Task.FromResult(new CompletionList(items));
        }
    }
}