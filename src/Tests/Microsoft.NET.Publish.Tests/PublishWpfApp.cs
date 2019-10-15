using System.IO;
using FluentAssertions;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using Microsoft.Extensions.DependencyModel;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishWpfApp : SdkTest
    {
        public PublishWpfApp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_publishes_and_runs_self_contained_wpf_app()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            string targetFramework = "netcoreapp3.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "wpf").Should().Pass();

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
