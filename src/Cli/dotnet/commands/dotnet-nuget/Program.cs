// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.NuGet
{
    public class NuGetCommand
    {
        public static int Run(string[] args)
        {
            return Run(args, new NuGetCommandRunner());
        }

        public static int Run(ParseResult parseResult)
        {
            return Run(parseResult.GetArguments(), new NuGetCommandRunner());
        }

        public static int Run(string[] args, ICommandRunner nugetCommandRunner)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            if (nugetCommandRunner == null)
            {
                throw new ArgumentNullException(nameof(nugetCommandRunner));
            }
            // replace -? with --help for NuGet CLI
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-?")
                    args[i] = "--help";
            }
            return nugetCommandRunner.Run(args);
        }

        private class NuGetCommandRunner : ICommandRunner
        {
            public int Run(string[] args)
            {
                var nugetApp = new NuGetForwardingApp(args);
                nugetApp.WithEnvironmentVariable("DOTNET_HOST_PATH", GetDotnetPath());
                return nugetApp.Execute();
            }
        }

        private static string GetDotnetPath()
        {
            return new Muxer().MuxerPath;
        }
    }
}
