using System;
using System.Collections.Generic;
using Microsoft.DotNet.Tools.Add;
using Microsoft.DotNet.Tools.Build;
using Microsoft.DotNet.Tools.Clean;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.List;
using Microsoft.DotNet.Tools.Migrate;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;
using Microsoft.DotNet.Tools.Pack;
using Microsoft.DotNet.Tools.Publish;
using Microsoft.DotNet.Tools.Remove;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Tools.Run;
using Microsoft.DotNet.Tools.Sln;
using Microsoft.DotNet.Tools.Test;
using Microsoft.DotNet.Tools.VSTest;
using Microsoft.DotNet.Tools.Cache;

namespace Microsoft.DotNet.Cli
{
    public static class BuiltInCommandsCatalog
    {
        public static Dictionary<string, BuiltInCommandMetadata> Commands = new Dictionary<string, BuiltInCommandMetadata>
        {
            ["add"] = new BuiltInCommandMetadata
            {
                Command = AddCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-add")
            },
            ["build"] = new BuiltInCommandMetadata
            {
                Command = BuildCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-build")
            },
            ["cache"] = new BuiltInCommandMetadata
            {
                Command = CacheCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-cache")
            },
            ["clean"] = new BuiltInCommandMetadata 
            {
                Command = CleanCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-clean")
            },
            ["help"] = new BuiltInCommandMetadata
            {
                Command = HelpCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-help")
            },
            ["list"] = new BuiltInCommandMetadata
            {
                Command = ListCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-list")
            },
            ["migrate"] = new BuiltInCommandMetadata
            {
                Command = MigrateCommand.Run,
                DocLink = new Uri("http://aka.ms/dotnet-migrate")

            },
            ["msbuild"] = new BuiltInCommandMetadata
            {
                Command = MSBuildCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-msbuild")
            },
            ["new"] = new BuiltInCommandMetadata
            {
                Command = NewCommandShim.Run,
                DocLink = new Uri("https://aka.ms/dotnet-new")
            },
            ["nuget"] = new BuiltInCommandMetadata
            {
                Command = NuGetCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-nuget")
            },
            ["pack"] = new BuiltInCommandMetadata
            {
                Command = PackCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-pack")
            },
            ["publish"] = new BuiltInCommandMetadata
            {
                Command = PublishCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-publish")
            },
            ["remove"] = new BuiltInCommandMetadata
            {
                Command = RemoveCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-remove")
            },
            ["restore"] = new BuiltInCommandMetadata
            {
                Command = RestoreCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-restore")
            },
            ["run"] = new BuiltInCommandMetadata
            {
                Command = RunCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-run")
            },
            ["sln"] = new BuiltInCommandMetadata
            {
                Command = SlnCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-sln")
            },
            ["test"] = new BuiltInCommandMetadata
            {
                Command = TestCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-test")
            },
            ["vstest"] = new BuiltInCommandMetadata
            {
                Command = VSTestCommand.Run,
                DocLink = new Uri("https://aka.ms/dotnet-vstest")
            }
        };

    }
}