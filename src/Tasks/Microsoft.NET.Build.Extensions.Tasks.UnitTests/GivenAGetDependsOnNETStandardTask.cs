// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetDependsOnNETStandardTask
    {
        [Fact]
        public void CanCheckThisAssembly()
        {
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardTask).GetTypeInfo().Assembly.Location;
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                References = new[] { new MockTaskItem() { ItemSpec = thisAssemblyPath } }
            };
            task.Execute().Should().BeTrue();

            // this test compiles against a sufficiently high System.Runtime
            task.DependsOnNETStandard.Should().BeTrue();
        }

        [Fact]
        public void CanCheckThisAssemblyByHintPath()
        {
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardTask).GetTypeInfo().Assembly.Location;
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                References = new[]
                {
                    new MockTaskItem(
                        Path.GetFileNameWithoutExtension(thisAssemblyPath),
                        new Dictionary<string, string>
                        {
                            {"HintPath", thisAssemblyPath }
                        })
                }
            };
            task.Execute().Should().BeTrue();

            // this test compiles against a sufficiently high System.Runtime
            task.DependsOnNETStandard.Should().BeTrue();
        }


        [Fact]
        public void ReturnsFalseForNonPE()
        {
            string testFile = $"testFile.{nameof(GivenAGetDependsOnNETStandardTask)}.{nameof(ReturnsFalseForNonPE)}.txt";
            try
            {
                File.WriteAllText(testFile, "test file");
                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    References = new[] { new MockTaskItem() { ItemSpec = testFile } }
                };
                // ensure that false is returned and no exception is thrown
                task.Execute().Should().BeTrue();
                task.DependsOnNETStandard.Should().BeFalse();
                // warn for a non-PE file
                ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().BeGreaterThan(0);
            }
            finally
            {
                File.Delete(testFile);
            }
        }

        [Fact]
        public void ReturnsFalseForNativeLibrary()
        {
            var corelibLocation = typeof(object).GetTypeInfo().Assembly.Location;
            var corelibFolder = Path.GetDirectoryName(corelibLocation);

            // do our best to try and find it, intentionally only use the windows name since linux will not be a PE.
            var coreclrLocation = Directory.EnumerateFiles(corelibFolder, "coreclr.dll").FirstOrDefault();

            // if we can't find it, skip the test
            if (coreclrLocation != null)
            {
                // ensure that false is returned and no warning is logged for native library
                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    References = new[] { new MockTaskItem() { ItemSpec = coreclrLocation } }
                };

                task.Execute().Should().BeTrue();
                task.DependsOnNETStandard.Should().BeFalse();
                // don't warn for PE with no metadata
                ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().Be(0);
            }
        }

        [Fact]
        public void SucceedsOnMissingFileReturnsFalse()
        {
            var missingFile = $"{nameof(SucceedsOnMissingFileReturnsFalse)}.shouldNotExist.dll";
            File.Exists(missingFile).Should().BeFalse();
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                References = new[] { new MockTaskItem() { ItemSpec = missingFile } }
            };

            task.Execute().Should().BeTrue();
            task.DependsOnNETStandard.Should().BeFalse();
            ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().Be(0);
        }

        [Fact]
        public void SucceedsWithWarningOnLockedFile()
        {
            var lockedFile = $"{nameof(SucceedsWithWarningOnLockedFile)}.dll";

            try
            {
                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    References = new[] { new MockTaskItem() { ItemSpec = lockedFile } }
                };

                // create file with no sharing
                using (var fileHandle = new FileStream(lockedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    task.Execute().Should().BeTrue();
                    task.DependsOnNETStandard.Should().BeFalse();
                    ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().BeGreaterThan(0);
                }
            }
            finally
            {
                File.Delete(lockedFile);
            }
        }
    }
}
