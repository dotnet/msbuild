// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ApplyDeltaTests : DotNetWatchTestBase
    {
        public ApplyDeltaTests(ITestOutputHelper logger)
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
            await App.AssertOutputLineStartsWith("Hello!");

            var newSrc = """
                public class Lib
                {
                    public static void Print()
                        => System.Console.WriteLine("Changed!");
                }
                """;

            File.WriteAllText(Path.Combine(dependencyDir, "Foo.cs"), newSrc);
            await App.AssertOutputLineStartsWith("Changed!");
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/35264")]
        public async Task HandleTypeLoadFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
                .WithSource()
                .Path;

            var projectDir = Path.Combine(testAsset, "App");

            App.DotnetWatchArgs.Add("--verbose");
            await App.StartWatcherAsync(projectDir);
            await App.AssertOutputLineStartsWith("Hello!");

            var newSrc = """
                class DepSubType : Dep
                {
                    int F() => 2;
                }

                class Printer
                {
                    public static void Print()
                    {
                        Console.WriteLine("Changed!");
                    }
                }
                """;

            File.WriteAllText(Path.Combine(projectDir, "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }
    }
}
