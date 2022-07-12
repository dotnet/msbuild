// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
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
