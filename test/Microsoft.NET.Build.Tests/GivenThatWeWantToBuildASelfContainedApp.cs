// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASelfContainedApp : SdkTest
    {
        public GivenThatWeWantToBuildASelfContainedApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_a_runnable_output()
        {
            var targetFramework = "netcoreapp1.1";
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                selfContainedExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            Command.Create(selfContainedExecutableFullPath, new string[] { })
                .EnsureExecutable()
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void It_errors_out_when_RuntimeIdentifier_architecture_and_PlatformTarget_do_not_match()
        {
			var testAsset = _testAssetsManager
				.CopyTestAsset("HelloWorld")
				.WithSource()
				.WithProjectChanges(project =>
				{
					var ns = project.Root.Name.Namespace;
					var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
					propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", "win10-x64"));
                    propertyGroup.Add(new XElement(ns + "PlatformTarget", "x86"));
				})
				.Restore(Log);

			var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot));

			buildCommand
				.Execute()
				.Should()
                .Fail();
        }

		[Fact]
		public void It_succeeds_when_RuntimeIdentifier_and_PlatformTarget_mismatch_but_PT_is_AnyCPU()
		{
			var targetFramework = "netcoreapp1.1";
			var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
			var testAsset = _testAssetsManager
				.CopyTestAsset("HelloWorld")
				.WithSource()
				.WithProjectChanges(project =>
				{
					var ns = project.Root.Name.Namespace;
					var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
					propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
					propertyGroup.Add(new XElement(ns + "PlatformTarget", "AnyCPU"));
				})
				.Restore(Log);

			var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot));

			buildCommand
				.Execute()
				.Should()
				.Pass();

			var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
			var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

			string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

			Command.Create(selfContainedExecutableFullPath, new string[] { })
				.EnsureExecutable()
				.CaptureStdOut()
				.Execute()
				.Should()
				.Pass()
				.And
				.HaveStdOutContaining("Hello World!");
		}
    }
}
