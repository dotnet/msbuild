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
                .WithSource();

            var dependencyDir = Path.Combine(testAsset.Path, "Dependency");

            await App.StartWatcherAsync(testAsset, "AppWithDeps");

            await App.WaitForSessionStarted();

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

        [Fact]
        public async Task HandleTypeLoadFailure()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppTypeLoadFailure")
                .WithSource();

            await App.StartWatcherAsync(testAsset, "App");

            await App.WaitForSessionStarted();

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

            File.WriteAllText(Path.Combine(testAsset.Path, "App", "Update.cs"), newSrc);

            await App.AssertOutputLineStartsWith("Updated types: Printer");
        }
    }
}
