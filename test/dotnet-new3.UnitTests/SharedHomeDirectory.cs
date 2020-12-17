using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace dotnet_new3.UnitTests
{
    /// <summary>
    /// This class represents shared /tmp/RANDOM-GUID/.templateengine/dotnetcli-preview/ folder
    /// shared between multiple unit tests in same class, this is so each test
    /// doesn't have to install everything from 0. To save some time executing tests.
    /// </summary>
    public class SharedHomeDirectory : IDisposable
    {
        private readonly ITestOutputHelper log;

        public string HomeDirectory { get; } = Helpers.CreateTemporaryFolder("Home");
        public string HomeVariable { get; } = Helpers.HomeEnvironmentVariableName;

        public SharedHomeDirectory(IMessageSink messageSink)
        {
            this.log = new SharedTestOutputHelper(messageSink);
            Initialize();
        }

        void Initialize()
        {
            var dn3Path = Environment.GetEnvironmentVariable("DN3");
            if (string.IsNullOrEmpty(dn3Path))
            {
                var path = typeof(AllProjectsWork).Assembly.Location;
                while (path != null && !File.Exists(Path.Combine(path, "Microsoft.TemplateEngine.sln")))
                {
                    path = Path.GetDirectoryName(path);
                }
                if (path == null)
                    throw new Exception("Couldn't find repository root, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.");
                dn3Path = path;
            }

            new DotnetNewCommand(log)
                .WithEnvironmentVariable(HomeVariable, HomeDirectory)
                .WithEnvironmentVariable("DN3", dn3Path)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        public void Dispose()
        {
            Directory.Delete(HomeDirectory, true);
        }


        HashSet<string> installedPackages = new HashSet<string>();
        public void InstallPackage(string packageName)
        {
            if (!installedPackages.Add(packageName))
                return;
            new DotnetNewCommand(log, "-i", packageName)
                .WithEnvironmentVariable(HomeVariable, HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }
    }
}
