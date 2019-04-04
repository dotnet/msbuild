// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithAllFeatures : SdkTest
    {
        public GivenThatWeWantToPublishAProjectWithAllFeatures(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [MemberData(nameof(PublishData))]
        public void It_publishes_the_project_correctly(string targetFramework, string [] expectedPublishFiles)
        {
            PublishCommand publishCommand = GetPublishCommand(targetFramework);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(targetFramework);

            publishDirectory.Should().OnlyHaveFiles(expectedPublishFiles);

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                // Ensure Newtonsoft.Json doesn't get excluded from the deps.json file.
                // TestLibrary has a hard dependency on Newtonsoft.Json.
                // TestApp has a PrivateAssets=All dependency on Microsoft.Extensions.DependencyModel, which depends on Newtonsoft.Json.
                // This verifies that P2P references get walked correctly when doing PrivateAssets exclusion.
                VerifyDependency(dependencyContext, "Newtonsoft.Json", "lib/netstandard1.0/", null);

                // Verify P2P references get created correctly in the .deps.json file.
                VerifyDependency(dependencyContext, "TestLibrary", "", null,
                    "da", "de", "fr");

                // Verify package reference with satellites gets created correctly in the .deps.json file
                VerifyDependency(dependencyContext, "Humanizer.Core", "lib/netstandard1.0/", "Humanizer",
                    "af", "ar", "bg", "bn-BD", "cs", "da", "de", "el", "es", "fa", "fi-FI", "fr", "fr-BE", "he", "hr",
                    "hu", "id", "it", "ja", "lv", "nb", "nb-NO", "nl", "pl", "pt", "ro", "ru", "sk", "sl", "sr",
                    "sr-Latn", "sv", "tr", "uk", "uz-Cyrl-UZ", "uz-Latn-UZ", "vi", "zh-CN", "zh-Hans", "zh-Hant");
            }

            var runtimeConfigJsonContents = File.ReadAllText(Path.Combine(publishDirectory.FullName, "TestApp.runtimeconfig.json"));
            var runtimeConfigJsonObject = JObject.Parse(runtimeConfigJsonContents);

            var baselineConfigJsonObject = JObject.Parse(@"{
    ""runtimeOptions"": {
        ""configProperties"": {
            ""System.GC.Concurrent"": false,
            ""System.GC.Server"": true,
            ""System.GC.RetainVM"": false,
            ""System.Runtime.TieredCompilation"": true,
            ""System.Runtime.TieredCompilation.QuickJit"": true,
            ""System.Threading.ThreadPool.MinThreads"": 2,
            ""System.Threading.ThreadPool.MaxThreads"": 9,
            ""System.Globalization.Invariant"": true,
            ""extraProperty"": true
        },
        ""framework"": {
            ""name"": ""Microsoft.NETCore.App"",
            ""version"": ""set below""
        },
        ""applyPatches"": true
    }
}");
            baselineConfigJsonObject["runtimeOptions"]["tfm"] = targetFramework;
            baselineConfigJsonObject["runtimeOptions"]["framework"]["version"] = 
                targetFramework == "netcoreapp1.0" ? "1.0.5" : "1.1.2";

            runtimeConfigJsonObject
                .Should()
                .BeEquivalentTo(baselineConfigJsonObject);
        }

        [Fact]
        public void It_fails_when_nobuild_is_set_and_build_was_not_performed_previously()
        {
            var publishCommand = GetPublishCommand("netcoreapp1.0").Execute("/p:NoBuild=true");
            publishCommand.Should().Fail().And.HaveStdOutContaining("MSB3030"); // "Could not copy ___ because it was not found."
        }

        [Theory]
        [MemberData(nameof(PublishData))]
        public void It_does_not_build_when_nobuild_is_set(string targetFramework, string[] expectedPublishFiles)
        {
            var publishCommand = GetPublishCommand(targetFramework);

            // do a separate build invocation before publish
            var buildCommand = new BuildCommand(Log, publishCommand.ProjectRootPath);
            buildCommand.Execute().Should().Pass();

            // modify all project files, which would force recompilation if we were to build during publish
            WaitForUtcNowToAdvance();
            foreach (string projectFile in EnumerateFiles(buildCommand, "*.csproj"))
            {
                File.AppendAllText(projectFile, " ");
            }

            // capture modification time of all binaries before publish
            var modificationTimes = GetLastWriteTimesUtc(buildCommand, "*.exe", "*.dll", "*.resources", "*.pdb");

            // publish (with NoBuild set)
            WaitForUtcNowToAdvance();
            publishCommand.Execute("/p:NoBuild=true").Should().Pass();
            publishCommand.GetOutputDirectory(targetFramework).Should().OnlyHaveFiles(expectedPublishFiles);

            // check that publish did not modify any of the build output
            foreach (var (file, modificationTime) in modificationTimes)
            {
                File.GetLastWriteTimeUtc(file)
                    .Should().Be(
                        modificationTime, 
                        because: $"Publish with NoBuild=true should not overwrite {file}");
            }
        }

        private static List<(string, DateTime)> GetLastWriteTimesUtc(MSBuildCommand command, params string[] searchPatterns)
        {
            return EnumerateFiles(command, searchPatterns)
                .Select(file => (file, File.GetLastWriteTimeUtc(file)))
                .ToList();
        }

        private static IEnumerable<string> EnumerateFiles(MSBuildCommand command, params string[] searchPatterns)
        {
            return searchPatterns.SelectMany(
                pattern => Directory.EnumerateFiles(
                    Path.Combine(command.ProjectRootPath, ".."), // up one level from TestApp to also get TestLibrary P2P files
                    pattern,
                    SearchOption.AllDirectories));
        }

        private PublishCommand GetPublishCommand(string targetFramework, [CallerMemberName] string callingMethod = null)
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink", callingMethod, identifier: targetFramework)
                .WithSource()
                .WithProjectChanges((path, project) =>
                {
                    if (Path.GetFileName(path).Equals("TestApp.csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var ns = project.Root.Name.Namespace;

                        var targetFrameworkElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single();
                        targetFrameworkElement.SetValue(targetFramework);
                    }
                });

            testAsset.Restore(Log, "TestApp");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            return new PublishCommand(Log, appProjectDirectory);
        }

        private static void VerifyDependency(
            DependencyContext dependencyContext,
            string name,
            string path,
            string dllName,
            params string[] locales)
        {
            if (string.IsNullOrEmpty(dllName))
            {
                dllName = name;
            }

            var library = dependencyContext
                .RuntimeLibraries
                .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

            library.Should().NotBeNull();
            library.RuntimeAssemblyGroups.Count.Should().Be(1);
            library.RuntimeAssemblyGroups[0].Runtime.Should().Be(string.Empty);
            library.RuntimeAssemblyGroups[0].AssetPaths.Count.Should().Be(1);
            library.RuntimeAssemblyGroups[0].AssetPaths[0].Should().Be($"{path}{dllName}.dll");
            
            foreach (string locale in locales)
            {
                // Try to get the locale as part of a dependency package: Humanizer.Core.af
                var localeLibrary = dependencyContext
                    .RuntimeLibraries
                    .FirstOrDefault(l => string.Equals(l.Name, $"{name}.{locale}", StringComparison.OrdinalIgnoreCase));

                if (!LocaleInSeparatePackage(localeLibrary))
                {
                    localeLibrary = library;
                }
                
                localeLibrary
                   .ResourceAssemblies
                   .FirstOrDefault(r => r.Locale == locale && r.Path == $"{path}{locale}/{dllName}.resources.dll")
                   .Should()
                   .NotBeNull();
            }
        }

        private static bool LocaleInSeparatePackage(RuntimeLibrary localeLibrary)
        {
            return localeLibrary != null;
        }

        public static IEnumerable<object[]> PublishData
        {
            get
            {
                yield return new object[] {
                    "netcoreapp1.0",
                    new string[]
                    {
                        "TestApp.dll",
                        "TestApp.pdb",
                        "TestApp.deps.json",
                        "TestApp.runtimeconfig.json",
                        "TestLibrary.dll",
                        "TestLibrary.pdb",
                        "Newtonsoft.Json.dll",
                        "System.Runtime.Serialization.Primitives.dll",
                        "CompileCopyToOutput.cs",
                        "Resource1.resx",
                        "ContentAlways.txt",
                        "ContentPreserveNewest.txt",
                        "NoneCopyOutputAlways.txt",
                        "NoneCopyOutputPreserveNewest.txt",
                        "CopyToOutputFromProjectReference.txt",
                        "Humanizer.dll",
                        "System.AppContext.dll",
                        "System.Buffers.dll",
                        "System.Collections.Concurrent.dll",
                        "System.Diagnostics.DiagnosticSource.dll",
                        "System.IO.Compression.ZipFile.dll",
                        "System.IO.FileSystem.Primitives.dll",
                        "System.Linq.dll",
                        "System.Linq.Expressions.dll",
                        "System.ObjectModel.dll",
                        "System.Reflection.Emit.dll",
                        "System.Reflection.Emit.ILGeneration.dll",
                        "System.Reflection.Emit.Lightweight.dll",
                        "System.Reflection.TypeExtensions.dll",
                        "System.Runtime.InteropServices.RuntimeInformation.dll",
                        "System.Runtime.Numerics.dll",
                        "System.Security.Cryptography.OpenSsl.dll",
                        "System.Security.Cryptography.Primitives.dll",
                        "System.Text.RegularExpressions.dll",
                        "System.Threading.dll",
                        "System.Threading.Tasks.Extensions.dll",
                        "System.Xml.ReaderWriter.dll",
                        "System.Xml.XDocument.dll",
                        "da/TestApp.resources.dll",
                        "da/TestLibrary.resources.dll",
                        "de/TestApp.resources.dll",
                        "de/TestLibrary.resources.dll",
                        "fr/TestApp.resources.dll",
                        "fr/TestLibrary.resources.dll",
                        "de/Humanizer.resources.dll",
                        "es/Humanizer.resources.dll",
                        "fr/Humanizer.resources.dll",
                        "it/Humanizer.resources.dll",
                        "ja/Humanizer.resources.dll",
                        "ru/Humanizer.resources.dll",
                        "zh-Hans/Humanizer.resources.dll",
                        "zh-Hant/Humanizer.resources.dll",
                        "zh-CN/Humanizer.resources.dll",
                        "vi/Humanizer.resources.dll",
                        "uz-Latn-UZ/Humanizer.resources.dll",
                        "uz-Cyrl-UZ/Humanizer.resources.dll",
                        "uk/Humanizer.resources.dll",
                        "tr/Humanizer.resources.dll",
                        "sv/Humanizer.resources.dll",
                        "sr-Latn/Humanizer.resources.dll",
                        "sr/Humanizer.resources.dll",
                        "sl/Humanizer.resources.dll",
                        "sk/Humanizer.resources.dll",
                        "ro/Humanizer.resources.dll",
                        "pt/Humanizer.resources.dll",
                        "pl/Humanizer.resources.dll",
                        "nl/Humanizer.resources.dll",
                        "nb-NO/Humanizer.resources.dll",
                        "nb/Humanizer.resources.dll",
                        "lv/Humanizer.resources.dll",
                        "id/Humanizer.resources.dll",
                        "hu/Humanizer.resources.dll",
                        "hr/Humanizer.resources.dll",
                        "he/Humanizer.resources.dll",
                        "fr-BE/Humanizer.resources.dll",
                        "fi-FI/Humanizer.resources.dll",
                        "fa/Humanizer.resources.dll",
                        "el/Humanizer.resources.dll",
                        "da/Humanizer.resources.dll",
                        "cs/Humanizer.resources.dll",
                        "bn-BD/Humanizer.resources.dll",
                        "bg/Humanizer.resources.dll",
                        "ar/Humanizer.resources.dll",
                        "af/Humanizer.resources.dll",
                        "runtimes/debian.8-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/fedora.23-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/fedora.24-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/opensuse.13.2-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/opensuse.42.1-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/osx/lib/netstandard1.6/System.Security.Cryptography.Algorithms.dll",
                        "runtimes/osx.10.10-x64/native/System.Security.Cryptography.Native.Apple.dylib",
                        "runtimes/osx.10.10-x64/native/System.Security.Cryptography.Native.OpenSsl.dylib",
                        "runtimes/rhel.7-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/ubuntu.14.04-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/ubuntu.16.04-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/ubuntu.16.10-x64/native/System.Security.Cryptography.Native.OpenSsl.so",
                        "runtimes/unix/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll",
                        "runtimes/unix/lib/netstandard1.3/System.Globalization.Extensions.dll",
                        "runtimes/unix/lib/netstandard1.3/System.IO.Compression.dll",
                        "runtimes/unix/lib/netstandard1.3/System.Security.Cryptography.Csp.dll",
                        "runtimes/unix/lib/netstandard1.3/System.Security.Cryptography.Encoding.dll",
                        "runtimes/unix/lib/netstandard1.6/System.Net.Http.dll",
                        "runtimes/unix/lib/netstandard1.6/System.Security.Cryptography.Algorithms.dll",
                        "runtimes/unix/lib/netstandard1.6/System.Security.Cryptography.Cng.dll",
                        "runtimes/unix/lib/netstandard1.6/System.Security.Cryptography.OpenSsl.dll",
                        "runtimes/unix/lib/netstandard1.6/System.Security.Cryptography.X509Certificates.dll",
                        "runtimes/win/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll",
                        "runtimes/win/lib/netstandard1.3/System.Globalization.Extensions.dll",
                        "runtimes/win/lib/netstandard1.3/System.IO.Compression.dll",
                        "runtimes/win/lib/netstandard1.3/System.Net.Http.dll",
                        "runtimes/win/lib/netstandard1.3/System.Security.Cryptography.Csp.dll",
                        "runtimes/win/lib/netstandard1.3/System.Security.Cryptography.Encoding.dll",
                        "runtimes/win/lib/netstandard1.6/System.Security.Cryptography.Algorithms.dll",
                        "runtimes/win/lib/netstandard1.6/System.Security.Cryptography.Cng.dll",
                        "runtimes/win/lib/netstandard1.6/System.Security.Cryptography.X509Certificates.dll",
                    }
                };

                yield return new object[] {
                    "netcoreapp1.1",
                    new string[]
                    {
                        "TestApp.dll",
                        "TestApp.pdb",
                        "TestApp.deps.json",
                        "TestApp.runtimeconfig.json",
                        "TestLibrary.dll",
                        "TestLibrary.pdb",
                        "Newtonsoft.Json.dll",
                        "System.Runtime.Serialization.Primitives.dll",
                        "CompileCopyToOutput.cs",
                        "Resource1.resx",
                        "ContentAlways.txt",
                        "ContentPreserveNewest.txt",
                        "NoneCopyOutputAlways.txt",
                        "NoneCopyOutputPreserveNewest.txt",
                        "CopyToOutputFromProjectReference.txt",
                        "da/TestApp.resources.dll",
                        "da/TestLibrary.resources.dll",
                        "de/TestApp.resources.dll",
                        "de/TestLibrary.resources.dll",
                        "fr/TestApp.resources.dll",
                        "fr/TestLibrary.resources.dll",
                        "Humanizer.dll",
                        "de/Humanizer.resources.dll",
                        "es/Humanizer.resources.dll",
                        "fr/Humanizer.resources.dll",
                        "it/Humanizer.resources.dll",
                        "ja/Humanizer.resources.dll",
                        "ru/Humanizer.resources.dll",
                        "zh-Hans/Humanizer.resources.dll",
                        "zh-Hant/Humanizer.resources.dll",
                        "zh-CN/Humanizer.resources.dll",
                        "vi/Humanizer.resources.dll",
                        "uz-Latn-UZ/Humanizer.resources.dll",
                        "uz-Cyrl-UZ/Humanizer.resources.dll",
                        "uk/Humanizer.resources.dll",
                        "tr/Humanizer.resources.dll",
                        "sv/Humanizer.resources.dll",
                        "sr-Latn/Humanizer.resources.dll",
                        "sr/Humanizer.resources.dll",
                        "sl/Humanizer.resources.dll",
                        "sk/Humanizer.resources.dll",
                        "ro/Humanizer.resources.dll",
                        "pt/Humanizer.resources.dll",
                        "pl/Humanizer.resources.dll",
                        "nl/Humanizer.resources.dll",
                        "nb-NO/Humanizer.resources.dll",
                        "nb/Humanizer.resources.dll",
                        "lv/Humanizer.resources.dll",
                        "id/Humanizer.resources.dll",
                        "hu/Humanizer.resources.dll",
                        "hr/Humanizer.resources.dll",
                        "he/Humanizer.resources.dll",
                        "fr-BE/Humanizer.resources.dll",
                        "fi-FI/Humanizer.resources.dll",
                        "fa/Humanizer.resources.dll",
                        "el/Humanizer.resources.dll",
                        "da/Humanizer.resources.dll",
                        "cs/Humanizer.resources.dll",
                        "bn-BD/Humanizer.resources.dll",
                        "bg/Humanizer.resources.dll",
                        "ar/Humanizer.resources.dll",
                        "af/Humanizer.resources.dll"
                    }
                };
            }
        }

    }
}
