using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.JsonRpc;
using System.Threading.Tasks;
using System.Threading;
using OmniSharp.Extensions.LanguageServer.Protocol;
using MediatR;
using System.Collections.Generic;

public class FormattingHandler : IJsonRpcRequestHandler<DocumentFormattingParams, TextEditContainer?>, IJsonRpcHandler
{
    public async Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
        CancellationToken cancellationToken)
    {
        var edits = new List<TextEdit>();
        return new TextEditContainer(edits);
    }
}
