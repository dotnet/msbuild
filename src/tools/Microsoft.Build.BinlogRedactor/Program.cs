using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BinlogRedactor.BinaryLog;
using Microsoft.Build.BinlogRedactor.Commands;
using Microsoft.Build.BinlogRedactor.Reporting;
using Microsoft.Build.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Build.BinlogRedactor
{
    internal sealed class Program
    {
        static Task<int> Main(string[] args)
        {
            return BuildCommandLine()
                .UseHost(
                _ => Host.CreateDefaultBuilder(),
                host =>
                {
                    host.ConfigureServices(services =>
                    {
                        services.AddSingleton<RedactBinlogCommandHandler>();
                        services.AddSingleton<IBinlogProcessor, SimpleBinlogProcessor>();
                        services.AddSingleton<IStderrWriter, DefaultStderrWriter>();
                        services.AddSingleton<IStdoutWriter, DefaultStdoutWriter>();
                        services.AddSingleton<IO.IFileSystem, IO.PhysicalFileSystem>();
                    })
                    .AddCancellationTokenProvider()
                    .ConfigureLogging(logging =>
                    {
                        logging.ConfigureBinlogRedactorLogging(host);
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
            var command = new RedactBinlogCommand();
            command.AddGlobalOption(CommonOptionsExtensions.s_consoleVerbosityOption);

            return new CommandLineBuilder(command);
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
