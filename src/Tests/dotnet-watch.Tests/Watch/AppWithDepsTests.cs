// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class AppWithDepsTests : DotNetWatchTestBase
    {
        public AppWithDepsTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task ChangeFileInDependency()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
                .WithSource()
                .Path;

            var projectDir = Path.Combine(testAsset, "AppWithDeps");
            var dependencyDir = Path.Combine(testAsset, "Dependency");

            await App.StartWatcherAsync(projectDir);

            var fileToChange = Path.Combine(dependencyDir, "Foo.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await App.AssertRestarted();
        }
    }
}
