using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BinlogRedactor.Reporting;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Build.BinlogRedactor
{
    internal sealed class Program
    {


        //private static void Main(string[] args)
        //{
        //    string binlogPath = "msbuild.binlog"; // args[0];

        //}

        static Task<int> Main(string[] args)
        {
            return BuildCommandLine()
                .UseHost(
                _ => Host.CreateDefaultBuilder(),
                host =>
                {
                    host.ConfigureServices(services =>
                    {
                        // services.AddSingleton<SomeCommandHandler>();
                        services.AddSingleton<IBinlogProcessor, SimpleBinlogProcessor>();
                    })
                    .AddCancellationTokenProvider()
                    .ConfigureLogging(logging =>
                    {
                        logging.ConfigureBuildLinkLogging(host);
                    });
                })
                .UseExceptionHandler(ExceptionHandler)
                .UseParseErrorReporting((int)BinlogRedactorErrorCode.InvalidOption)
                .CancelOnProcessTermination()
                .UseHelp()
                .UseDefaults()
                .EnablePosixBundling(true)
                .Build()
                .InvokeAsync(args);
        }

        private static CommandLineBuilder BuildCommandLine()
        {
            var root = new RootCommand("binlog-redactor - provides ability to redact sensitive data from MSBuild binlogs.");

            root.AddCommand(new GetSourcesCommand());
            root.AddCommand(new AddBuildMetadataCommand());
            root.AddCommand(new SourcePackageCommand());
            root.AddGlobalOption(CommonOptionsExtension.s_consoleVerbosityOption);
            root.AddGlobalOption(CommonOptionsExtension.s_fileVerbosityOption);

            return new CommandLineBuilder(root);
        }

        private static void ExceptionHandler(Exception exception, InvocationContext context)
        {
            if (exception is TargetInvocationException)
            {
                exception = exception.InnerException ?? exception;
            }

            ILogger? logger = context.BindingContext.GetService<ILogger<Program>>();
            logger.LogCritical(exception, "Unhandled exception occurred ({type})", exception.GetType());
            context.ExitCode = (int)BinlogRedactorErrorCode.InternalError;
        }
    }
}
