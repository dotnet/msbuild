// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class AppWithDepsTests
    {
        private readonly ITestOutputHelper _logger;

        public AppWithDepsTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async Task ChangeFileInDependency()
        {
            var testAssetsManager = new TestAssetsManager(_logger);

            var testAsset = testAssetsManager.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource()
                .Path;

            var projectDir= Path.Combine(testAsset, "AppWithDeps");
            var dependencyDir = Path.Combine(testAsset, "Dependency");

            using var app = new WatchableApp(projectDir, _logger);
            await app.StartWatcherAsync();

            var fileToChange = Path.Combine(dependencyDir, "Foo.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await app.HasRestarted();
        }
    }
}
