using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

// ReSharper disable UnusedParameter.Local

namespace RcmServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            var cacheService = new Cache();

            IObserver<WorkDoneProgressReport> workDone = null!;

            LanguageServer? server = await LanguageServer.From(
                options =>
                    options
                       .WithInput(Console.OpenStandardInput())
                       .WithOutput(Console.OpenStandardOutput())
                       .ConfigureLogging(
                            x => x
                                .AddLanguageProtocolLogging()
                            .SetMinimumLevel(LogLevel.Debug)
                )
                        .WithHandler<CompletionHandler>()
                        //.WithHandler<FormattingHandler>()
                        //.WithHandler<DidChangeWatchedFilesHandler>()
                        //.WithHandler<FoldingRangeHandler>()
                        //.WithHandler<MyWorkspaceSymbolsHandler>()
                        //.WithHandler<MyDocumentSymbolHandler>()
                        //.WithHandler<SemanticTokensHandler>()
                        .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
                        .WithServices(
                            services =>
                            {
                                services.AddSingleton(
                                    cache =>
                                    {
                                        return cacheService;
                                    }
                                );
                            }
                        )
                       .OnInitialize(
                            async (server, request, token) =>
                            {
                                await Task.Delay(2000).ConfigureAwait(false);
                            }
                        )
                       .OnStarted(
                            async (languageServer, token) =>
                            {
                               
                            }
                        )
            ).ConfigureAwait(false);

            var handlerUtils = new TextDocumentUtils(cacheService);
            var documentHandler = new TextDocumentHandler(server, handlerUtils, server.Configuration);
            server.Register(registry => { registry.AddHandler(documentHandler); });

            await server.WaitForExit.ConfigureAwait(false);
        }
    }
}