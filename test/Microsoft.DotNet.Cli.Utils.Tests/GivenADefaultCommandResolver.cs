// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
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
    public class GivenADefaultCommandResolver
    {
        [Fact]
        public void It_contains_resolvers_in_the_right_order()
        {
            var defaultCommandResolver = DefaultCommandResolver.Create();

            var resolvers = defaultCommandResolver.OrderedCommandResolvers;

            resolvers.Should().HaveCount(5);

            resolvers.Select(r => r.GetType())
                .Should()
                .ContainInOrder(
                    new []{
                        typeof(RootedCommandResolver),
                        typeof(ProjectDependenciesCommandResolver),
                        typeof(ProjectToolsCommandResolver),
                        typeof(AppBaseCommandResolver),
                        typeof(PathCommandResolver)
                    });
        }


        // [Fact]
        // public void It_Resolves_Rooted_Commands_Correctly()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, "rooteddir");
        //     Directory.CreateDirectory(path);

        //     var testCommandPath = CreateTestCommandFile(path, ".dll", "rootedcommand");

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandPath,
        //         CommandArguments = new string[] {}
        //     };

        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.RootedPath);
        // }

        // [Fact]
        // public void It_Resolves_AppBase_Commands_Correctly()
        // {
        //     var testCommandPath = CreateTestCommandFile(AppContext.BaseDirectory, ".exe", "appbasecommand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.BaseDirectory);
        // }

        // [Fact]
        // public void It_Resolves_PATH_Commands_Correctly()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, "pathdir");
        //     var testCommandPath = CreateTestCommandFile(path, ".dll", "pathcommmand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     Mock<IEnvironmentProvider> mockEnvironment = new Mock<IEnvironmentProvider>();
        //     mockEnvironment.Setup(c => c
        //         .GetCommandPath(It.IsAny<string>(), It.IsAny<string[]>()))
        //         .Returns(testCommandPath);

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         Environment = mockEnvironment.Object
        //     };

        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().Be(testCommandPath);
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.Path);
        // }

        // [Fact]
        // public void It_Resolves_Project_Tools_Commands_Correctly()
        // {
        //     var testAppPath = Path.Combine(AppContext.BaseDirectory, 
        //         "TestAssets/TestProjects/AppWithToolDependency");

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = "dotnet-hello",
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = testAppPath,
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().NotBeNull();
        //     commandSpec.Args.Should().NotContain("--depsfile");
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.NugetPackage);
        // }

        // [Fact]
        // public void It_Resolves_Project_Dependencies_Commands_Correctly()
        // {
        //     var testAppPath = Path.Combine(AppContext.BaseDirectory, 
        //         "TestAssets/TestProjects/AppWithDirectDependency");

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = "dotnet-hello",
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = testAppPath,
        //         Environment = new EnvironmentProvider(),
        //         Framework = FrameworkConstants.CommonFrameworks.DnxCore50,
        //         Configuration = "Debug"
        //     };


        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);
        //     commandSpec.Should().NotBeNull();

        //     commandSpec.Path.Should().NotBeNull();
        //     commandSpec.Args.Should().Contain("--depsfile");
        //     commandSpec.ResolutionStrategy.Should().Be(CommandResolutionStrategy.NugetPackage);
        // }

        // [Fact]
        // public void It_does_not_resolve_ProjectLocal_commands()
        // {
        //     var path = Path.Combine(AppContext.BaseDirectory, 
        //         "testdir");

        //     var testCommandPath = CreateTestCommandFile(path, ".exe", "projectlocalcommand");
        //     var testCommandName = Path.GetFileNameWithoutExtension(testCommandPath);

        //     var defaultCommandResolver = new DefaultCommandResolver();

        //     var commandResolverArgs = new CommandResolverArguments
        //     {
        //         CommandName = testCommandName,
        //         CommandArguments = new string[] {}, 
        //         ProjectDirectory = path,
        //         Environment = new EnvironmentProvider()
        //     };

        //     var commandSpec = defaultCommandResolver.Resolve(commandResolverArgs);

        //     commandSpec.Should().Be(null);
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
