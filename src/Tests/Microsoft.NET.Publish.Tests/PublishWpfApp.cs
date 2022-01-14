// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishWpfApp : SdkTest
    {
        public PublishWpfApp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_publishes_and_runs_self_contained_wpf_app()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "wpf", "--debug:ephemeral-hive").Should().Pass();

            var project = XDocument.Load(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj"));
            var ns = project.Root.Name.Namespace;
            string targetFramework = project.Root.Elements(ns + "PropertyGroup")
                .Elements(ns + "TargetFramework")
                .Single().Value;

            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            string mainWindowXamlCsPath = Path.Combine(testDir.Path, "MainWindow.xaml.cs");
            string csContents = File.ReadAllText(mainWindowXamlCsPath);
            csContents = csContents.Replace("InitializeComponent();", @"InitializeComponent();

    this.Loaded += delegate { Application.Current.Shutdown(42); };");

            File.WriteAllText(mainWindowXamlCsPath, csContents);

            var restoreCommand = new RestoreCommand(Log, testDir.Path);
            restoreCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(Log, testDir.Path);

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            var runAppCommand = new SdkCommandSpec()
            {
                FileName = Path.Combine(publishDirectory.FullName, Path.GetFileName(testDir.Path) + ".exe")
            };

            runAppCommand.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);

            var result = runAppCommand.ToCommand()
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            result.ExitCode.Should().Be(42);
        }
    }
}
