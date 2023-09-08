// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(testDir.Path)
                .Execute("wpf")
                .Should()
                .Pass();

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

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should()
                .Pass();

            var publishDirectory = OutputPathCalculator.FromProject(Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj")).GetPublishDirectory(
                targetFramework: targetFramework,
                runtimeIdentifier: rid);

            var runAppCommand = new SdkCommandSpec()
            {
                FileName = Path.Combine(publishDirectory, Path.GetFileName(testDir.Path) + ".exe")
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
