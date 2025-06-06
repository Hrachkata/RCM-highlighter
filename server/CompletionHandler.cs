﻿using MediatR;
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

        public CompletionHandler(Cache cache)
        {
            _cache = cache;
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            if (request.Position.Line > _cache.ScriptLine)
            {
                // Script territory no need to provide autocompletes, its JS
                return Task.FromResult(new CompletionList(true));
            }

            var items = new List<CompletionItem>(VibeHelper.Completions);

            var currentLine = _cache.GetLine(request.Position.Line);

            currentLine = currentLine.Replace("\"", "-");

            var templateLiteralRegex = new Regex(@"(Template.*?=.*?)(.).*(-)");

            var match = templateLiteralRegex.Match(currentLine);

            if (match.Groups.Count != 4)
            {
                return Task.FromResult(new CompletionList(items));
            }

            var positionFirstQuote = templateLiteralRegex.Match(currentLine).Groups[2].Index + 2;
            var positionSecondQuote = templateLiteralRegex.Match(currentLine).Groups[3].Index + 2;

            if (positionFirstQuote < request.Position.Character && positionSecondQuote > request.Position.Character)
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
    }
}