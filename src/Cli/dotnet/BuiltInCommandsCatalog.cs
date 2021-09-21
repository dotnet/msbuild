// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Sdk;
using Microsoft.DotNet.Tools.List;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;
using Microsoft.DotNet.Tools.Pack;
using Microsoft.DotNet.Tools.Publish;
using Microsoft.DotNet.Tools.Remove;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Tools.Run;
using Microsoft.DotNet.Tools.Sln;
using Microsoft.DotNet.Tools.Store;
using Microsoft.DotNet.Tools.Test;
using Microsoft.DotNet.Tools.VSTest;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.Tool;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Tools.Format;

namespace Microsoft.DotNet.Cli
{
    public static class BuiltInCommandsCatalog
    {
        public static Dictionary<string, BuiltInCommandMetadata> Commands = new Dictionary<string, BuiltInCommandMetadata>
        {
            ["new"] = new BuiltInCommandMetadata
            {
                Command = NewCommandShim.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-new
                DocLink = "https://aka.ms/dotnet-new"
            },
            ["nuget"] = new BuiltInCommandMetadata
            {
                Command = NuGetCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-nuget-locals
                DocLink = "https://aka.ms/dotnet-nuget"
            },
            ["pack"] = new BuiltInCommandMetadata
            {
                Command = PackCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-pack
                DocLink = "https://aka.ms/dotnet-pack"
            },
            ["publish"] = new BuiltInCommandMetadata
            {
                Command = PublishCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-publish
                DocLink = "https://aka.ms/dotnet-publish"
            },
            ["remove"] = new BuiltInCommandMetadata
            {
                Command = RemoveCommand.Run,
                // aka.ms link: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-remove-reference
                DocLink = "https://aka.ms/dotnet-remove"
            },
            ["restore"] = new BuiltInCommandMetadata
            {
                Command = RestoreCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-restore
                DocLink = "https://aka.ms/dotnet-restore"
            },
            ["run"] = new BuiltInCommandMetadata
            {
                Command = RunCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-run
                DocLink = "https://aka.ms/dotnet-run"
            },
            ["sdk"] = new BuiltInCommandMetadata
            {
                Command = SdkCommand.Run,
                DocLink = "https://aka.ms/dotnet-sdk"
            },
            ["sln"] = new BuiltInCommandMetadata
            {
                Command = SlnCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-sln
                DocLink = "https://aka.ms/dotnet-sln"
            },
            ["store"] = new BuiltInCommandMetadata
            {
                Command = StoreCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-store
                DocLink = "https://aka.ms/dotnet-store"
            },
            ["test"] = new BuiltInCommandMetadata
            {
                Command = TestCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-test
                DocLink = "https://aka.ms/dotnet-test"
            },
            ["vstest"] = new BuiltInCommandMetadata
            {
                Command = VSTestCommand.Run,
                // aka.ms target: https://docs.microsoft.com/dotnet/articles/core/tools/dotnet-vstest
                DocLink = "https://aka.ms/dotnet-vstest"
            },
            ["tool"] = new BuiltInCommandMetadata
            {
                Command = ToolCommand.Run,
                DocLink = "https://aka.ms/dotnet-tool"
            },
            ["workload"] = new BuiltInCommandMetadata
            {
                Command = WorkloadCommand.Run,
                DocLink = "https://aka.ms/dotnet-workload"
            },
        };
    }
}
