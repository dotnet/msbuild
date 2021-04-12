// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace dotnet_new3.IntegrationTests
{
    /// <summary>
    /// This class represents shared /tmp/RANDOM-GUID/.templateengine/dotnetcli-preview/ folder
    /// shared between multiple unit tests in same class, this is so each test
    /// doesn't have to install everything from 0. To save some time executing tests.
    /// </summary>
    public class SharedHomeDirectory : IDisposable
    {
        private readonly HashSet<string> _installedPackages = new HashSet<string>();

        public SharedHomeDirectory(IMessageSink messageSink)
        {
            Log = new SharedTestOutputHelper(messageSink);
            Initialize();
        }

        public string HomeDirectory { get; } = TestUtils.CreateTemporaryFolder("Home");

        protected ITestOutputHelper Log { get; private set; }

        public void Dispose() => Directory.Delete(HomeDirectory, true);

        public void InstallPackage(string packageName, string workingDirectory = null, string nugetSource = null)
        {
            if (!_installedPackages.Add(packageName))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            var args = new List<string> { "-i", packageName, };
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
            var dn3Path = Environment.GetEnvironmentVariable("DN3");
            if (string.IsNullOrEmpty(dn3Path))
            {
                var path = typeof(AllProjectsWork).Assembly.Location;
                while (path != null && !File.Exists(Path.Combine(path, "Microsoft.TemplateEngine.sln")))
                {
                    path = Path.GetDirectoryName(path);
                }
                dn3Path = path ?? throw new Exception("Couldn't find repository root, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.");
            }

            new DotnetNewCommand(Log)
                .WithCustomHive(HomeDirectory)
                .WithEnvironmentVariable("DN3", dn3Path)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }
    }
}
