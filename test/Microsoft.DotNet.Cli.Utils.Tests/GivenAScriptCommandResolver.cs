// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Moq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Threading;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAScriptCommandResolver
    {
        [Fact]
        public void It_contains_resolvers_in_the_right_order()
        {
            var scriptCommandResolver = ScriptCommandResolver.Create();

            var resolvers = scriptCommandResolver.OrderedCommandResolvers;

            resolvers.Should().HaveCount(4);

            resolvers.Select(r => r.GetType())
                .Should()
                .ContainInOrder(
                    new []{
                        typeof(RootedCommandResolver),
                        typeof(ProjectPathCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver)
                    });
        }

        // [Fact]
        // public void It_Resolves_Rooted_Commands_Correctly()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, "rooteddir");
        //     Directory.CreateDirectory(path);

        //     var testCommandPath = CreateTestCommandFile(path, ".dll", "scriptrootedcommand");

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandPath,
        //         CommandArguments = new string[] {}
        //     };

        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.RootedPath);
        // }

        // [Fact]
        // public void It_Resolves_AppBase_Commands_Correctly()
        // {
        //     var testCommandPath = CreateTestCommandFile(AppContext.BaseDirectory, ".exe", "scriptappbasecommand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.BaseDirectory);
        // }

        // [Fact]
        // public void It_Resolves_PATH_Commands_Correctly()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, "pathdir");
        //     var testCommandPath = CreateTestCommandFile(path, ".dll", "scriptpathcommmand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     Mock<IEnvironmentProvider> mockEnvironment = new Mock<IEnvironmentProvider>();
        //     mockEnvironment.Setup(c => c
        //         .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
        //         .Returns(testCommandPath);

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         Environment = mockEnvironment.Object
        //     };

        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.Path);
        // }

        // [Fact]
        // public void It_does_NOT_Resolve_Project_Tools_Commands()
        // {
        //     var testAppPath = Path.Combine(AppContext.BaseDirectory, 
        //         "TestAssets/TestProjects/AppWithToolDependency");

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = "dotnet-hello",
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = testAppPath,
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().BeNull();
        // }

        // [Fact]
        // public void It_does_NOT_Resolve_Project_Dependencies_Commands()
        // {
        //     var testAppPath = Path.Combine(AppContext.BaseDirectory, 
        //         "TestAssets/TestProjects/AppWithDirectDependency");

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = "dotnet-hello",
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = testAppPath,
        //         Environment = new EnvironmentProvider(),
        //         Framework = FrameworkConstants.CommonFrameworks.DnxCore50,
        //         Configuration = "Debug"
        //     };


        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().BeNull();
        // }

        // [Fact]
        // public void It_resolves_ProjectLocal_commands_correctly()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, 
        //         "testdir");

        //     var testCommandPath = CreateTestCommandFile(path, ".exe", "scriptprojectlocalcommand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     var scriptCommandResolver = new ScriptCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = path,
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = scriptCommandResolver.Resolve(commandResolverArgs);

        //     commandSpec.Should().NotBeNull();
        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);

        // }

        // public string CreateTestCommandFile(string path, string extension, string name = "testcommand")
        // {
        //     Directory.CreateDirectory(path);

        //     var filename = name + extension;
        //     var filepath = Path.Combine(path, filename);

        //     File.WriteAllText(filepath, "hello world");

        //     return filepath;
        // }
    }
}
