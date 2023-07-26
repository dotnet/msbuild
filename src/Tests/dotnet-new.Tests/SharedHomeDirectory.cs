// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedTestOutputHelper = Microsoft.TemplateEngine.TestHelper.SharedTestOutputHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    /// <summary>
    /// This class represents shared /tmp/RANDOM-GUID/.templateengine/dotnetcli-preview/ folder
    /// shared between multiple unit tests in same class, this is so each test
    /// doesn't have to install everything from 0. To save some time executing tests.
    /// </summary>
    public class SharedHomeDirectory : IDisposable
    {
        private readonly HashSet<string> _installedPackages = new();

        public SharedHomeDirectory(IMessageSink messageSink)
        {
            Log = new SharedTestOutputHelper(messageSink);
            Log.WriteLine("Initializing SharedHomeDirectory for folder {0}", HomeDirectory);
            Initialize();
        }

        public string HomeDirectory { get; } = Utilities.CreateTemporaryFolder(nameof(SharedHomeDirectory));

        protected ITestOutputHelper Log { get; private set; }

        public void Dispose()
        {
            Directory.Delete(HomeDirectory, true);
            GC.SuppressFinalize(this);
        }

        public void InstallPackage(string packageName, string? workingDirectory = null, string? nugetSource = null)
        {
            if (!_installedPackages.Add(packageName))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            List<string> args = new() { "install", packageName };
            if (!string.IsNullOrWhiteSpace(nugetSource))
            {
                args.AddRange(new[] { "--nuget-source", nugetSource });
            }
            new DotnetNewCommand(Log, args.ToArray())
                .WithCustomHive(HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        private void Initialize()
        {
            new DotnetNewCommand(Log)
                .WithCustomHive(HomeDirectory)
                .WithDebug()
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(Log, "install", TemplatePackagesPaths.MicrosoftDotNetCommonProjectTemplates60Path)
                .WithCustomHive(HomeDirectory)
                .WithDebug()
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(Log, "install", TemplatePackagesPaths.MicrosoftDotNetCommonProjectTemplates70Path)
                .WithCustomHive(HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }
    }
}
