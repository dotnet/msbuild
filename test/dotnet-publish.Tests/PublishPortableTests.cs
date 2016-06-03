using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishPortableTests : TestBase
    {
        private static readonly IEnumerable<Tuple<string, string>> ExpectedRuntimeOutputs = new[] {
            Tuple.Create("debian-x64", "libuv.so"),
            Tuple.Create("rhel-x64", "libuv.so"),
            Tuple.Create("osx", "libuv.dylib"),
            Tuple.Create("win7-arm", "libuv.dll"),
            Tuple.Create("win7-x86", "libuv.dll"),
            Tuple.Create("win7-x64", "libuv.dll")
        };

        [Fact]
        public void PortableAppWithRuntimeTargetsIsPublishedCorrectly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var publishDir = Publish(testInstance);

            publishDir.Should().HaveFiles(new[]
            {
                "PortableAppWithNative.dll",
                "PortableAppWithNative.deps.json"
            });

            // Prior to `type:platform` trimming, this would have been published.
            publishDir.Should().NotHaveFile("System.Linq.dll");

            // PortableAppWithNative references a Libuv version that is explicitly
            // not what is in Microsoft.NETCore.App.
            var runtimesOutput = publishDir.Sub("runtimes");

            runtimesOutput.Should().Exist();

            foreach (var output in ExpectedRuntimeOutputs)
            {
                var ridDir = runtimesOutput.Sub(output.Item1);
                ridDir.Should().Exist();

                var nativeDir = ridDir.Sub("native");
                nativeDir.Should().Exist();
                nativeDir.Should().HaveFile(output.Item2);
            }
        }

        [Fact]
        public void PortableAppWithIntentionalDowngradePublishesDowngradedManagedCode()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var publishCommand = new PublishCommand(Path.Combine(testInstance.TestRoot, "PortableAppWithIntentionalManagedDowngrade"));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(portable: true);
            publishDir.Should().HaveFiles(new[]
            {
                "PortableAppWithIntentionalManagedDowngrade.dll",
                "PortableAppWithIntentionalManagedDowngrade.deps.json",
                "System.Linq.dll"
            });
        }

        [Fact]
        public void PortableAppWithRuntimeTargetsDoesNotHaveRuntimeConfigDevJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var publishDir = Publish(testInstance);

            publishDir.Should().NotHaveFile("PortableAppWithNative.runtimeconfig.dev.json");
        }

        [Fact]
        public void RefsPublishTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("PortableTests")
                                                     .WithLockFiles();

            var publishCommand = new PublishCommand(Path.Combine(instance.TestRoot, "PortableAppCompilationContext"));
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory(true).Should().HaveFile("PortableAppCompilationContext.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishCommand.GetOutputDirectory(true).FullName, "refs"));
            // Microsoft.CodeAnalysis.CSharp is IL only
            refsDirectory.Should().NotHaveFile("Microsoft.CodeAnalysis.CSharp.dll");
            // System.IO has facede
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("PortableAppCompilationContext.dll");
        }

        private DirectoryInfo Publish(TestInstance testInstance)
        {
            var publishCommand = new PublishCommand(Path.Combine(testInstance.TestRoot, "PortableAppWithNative"));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            return publishCommand.GetOutputDirectory(portable: true);
        }
    }
}
