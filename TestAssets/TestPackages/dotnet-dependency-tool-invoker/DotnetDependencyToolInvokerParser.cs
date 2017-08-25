// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.DependencyInvoker
{
    internal static class DotnetDependencyToolInvokerParser
    {
        public static Microsoft.DotNet.Cli.CommandLine.Command DotnetDependencyToolInvoker() =>
            Create.Command(
                "dotnet-dependency-tool-invoker",
                "DotNet Dependency Tool Invoker",
                Accept.ExactlyOneArgument()
                      .With(name: "COMMAND",
                            description: "The command to execute."),
                false,
                Create.Option(
                    "-h|--help",
                    "Show help information",
                    Accept.NoArguments()),
                Create.Option(
                    "-p|--project-path",
                    "Path to Project.json that contains the tool dependency",
                    Accept.ExactlyOneArgument()
                          .With(name: "PROJECT_JSON_PATH",
                                defaultValue: () =>
                                    PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()))),
                Create.Option(
                    "-c|--configuration",
                    "Configuration under which to build",
                    Accept.ExactlyOneArgument()
                        .With(name: "CONFIGURATION",
                              defaultValue: () => Constants.DefaultConfiguration)),
                Create.Option(
                    "-o|--output",
                    "Directory in which to find the binaries to be run",
                    Accept.ExactlyOneArgument()
                          .With(name: "OUTPUT_DIR")),
                Create.Option(
                    "-f|--framework",
                    "Looks for test binaries for a specific framework",
                    Accept.ExactlyOneArgument()
                        .With(name: "FRAMEWORK")
                        .MaterializeAs(p => NuGetFramework.Parse(p.Arguments.Single()))),
                Create.Option(
                    "-r|--runtime",
                    "Look for test binaries for a for the specified runtime",
                    Accept.ExactlyOneArgument()
                        .With(name: "RUNTIME_IDENTIFIER")));
    }
}
