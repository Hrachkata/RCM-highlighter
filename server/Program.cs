using System;
using System.Threading.Tasks;
using System.Xml.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
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
            // Useless, remove later, logging is for nerds
            Log.Logger = new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                        .MinimumLevel.Verbose()
                        .CreateLogger();
            Log.Logger.Information("The server starts at least.");

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
                        .WithHandler<FormattingHandler>()
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
                                    schemaManger =>
                                    {
                                        var schemaManager = new SchemaManager();
                                        schemaManager.GetSchemaAsync("https://www.cozyroc.com/sites/default/files/down/schema/rcm-config-1.0.xsd").Wait();
                                        return schemaManager;
                                    }
                                ).AddSingleton(
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
                                //await Task.Delay(2000).ConfigureAwait(false);
                            }
                        )
                       .OnStarted(
                            async (languageServer, token) =>
                            {
                               
                            }
                        )
            ).ConfigureAwait(false);

            var handlerUtils = new TextDocumentUtils(cacheService);
            var documentHandler = new TextDocumentHandler(server, Log.Logger, handlerUtils, server.Configuration);
            server.Register(registry => { registry.AddHandler(documentHandler); });

            await server.WaitForExit.ConfigureAwait(false);
        }
    }

    public class Foo
    {
        private readonly Serilog.ILogger _logger;

        public Foo(Serilog.ILogger logger)
        {
            logger.Information("inside ctor of Foo");
            _logger = logger;
        }

        public void SayFoo()
        {
            _logger.Information("Fooooo!");
        }
    }
}