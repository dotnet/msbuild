// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedTestOutputHelper = Microsoft.TemplateEngine.TestHelper.SharedTestOutputHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class TemplateDiscoveryTool : IDisposable
    {
        private readonly string dotnetNewTestExecutionDir;
        private readonly SharedTestOutputHelper testOutputHelper;

        public TemplateDiscoveryTool(IMessageSink messageSink)
        {
            testOutputHelper = new SharedTestOutputHelper(messageSink);
            string home = Utilities.CreateTemporaryFolder("home");
            dotnetNewTestExecutionDir = Utilities.GetTestExecutionTempFolder();
            string toolManifestPath = Path.Combine(dotnetNewTestExecutionDir, @".config\dotnet-tools.json");
            if (!File.Exists(toolManifestPath))
            {
                new DotnetNewCommand(
                    testOutputHelper,
                    "tool-manifest")
                    .WithCustomHive(home)
                    .WithWorkingDirectory(dotnetNewTestExecutionDir)
                    .Execute()
                    .Should()
                    .Pass();
            }
            new DotnetToolCommand(
                testOutputHelper,
                "install",
                "Microsoft.TemplateSearch.TemplateDiscovery",
                "--version",
                TemplatePackageVersion.MicrosoftTemplateSearchTemplateDiscoveryPackageVersion)
                .WithWorkingDirectory(dotnetNewTestExecutionDir)
                .Execute()
                .Should()
                .Pass();
        }

        public AndConstraint<CommandResultAssertions> Run(ITestOutputHelper log, params string[] args)
        {
            var arguments = new List<string>();
            arguments.Add("run");
            arguments.Add("Microsoft.TemplateSearch.TemplateDiscovery");
            arguments.AddRange(args);
            return new DotnetToolCommand(log, arguments.ToArray())
                .WithWorkingDirectory(dotnetNewTestExecutionDir)
                .Execute()
                .Should()
                .ExitWith(0);
        }

        public void Dispose()
        {
        }
    }
}
