using System;
using System.Threading.Tasks;
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
            Log.Logger = new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                        .MinimumLevel.Verbose()
                        .CreateLogger();

            Log.Logger.Information("The server starts at least.");

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
                                    provider =>
                                    {
                                        return new Foo(Log.Logger);
                                    }
                                );
                                services.AddSingleton(
                                    new ConfigurationItem
                                    {
                                        Section = "xml",
                                    }
                                ).AddSingleton(
                                    new ConfigurationItem
                                    {
                                        Section = "terminal",
                                    }
                                );
                            }
                        )
                       .OnStarted(
                            async (languageServer, token) =>
                            {
                                using var manager = await languageServer.WorkDoneManager.Create(new WorkDoneProgressBegin { Title = "Doing some work..." })
                                                                        .ConfigureAwait(false);


                                var logger = languageServer.Services.GetService<ILogger<Foo>>();
                                var configuration = await languageServer.Configuration.GetConfiguration(
                                    new ConfigurationItem
                                    {
                                        Section = "xml",
                                    }, new ConfigurationItem
                                    {
                                        Section = "terminal",
                                    }
                                ).ConfigureAwait(false);

                                var baseConfig = new JObject();
                                foreach (var config in languageServer.Configuration.AsEnumerable())
                                {
                                    baseConfig.Add(config.Key, config.Value);
                                }

                                logger.LogInformation("Base Config: {@Config}", baseConfig);

                                var scopedConfig = new JObject();
                                foreach (var config in configuration.AsEnumerable())
                                {
                                    scopedConfig.Add(config.Key, config.Value);
                                }

                                logger.LogInformation("Scoped Config: {@Config}", scopedConfig);
                            }
                        )
            ).ConfigureAwait(false);

            var foo = new Foo(Log.Logger);
            var test = new TextDocumentHandler(server, Log.Logger, foo, server.Configuration);;

            server.Register(registry => { registry.AddHandler(test); });

            //var test = new ChangeHandler(server);
            //
            //server.HandlersManager.Add(test);

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