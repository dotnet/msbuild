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
                VerifyDependency(dependencyContext, "Newtonsoft.Json", targetFramework == "net6.0" ? "lib/netstandard2.0/" : "lib/netstandard1.3/", null);

                // Verify P2P references get created correctly in the .deps.json file.
                VerifyDependency(dependencyContext, "TestLibrary", "", null,
                    "da", "de", "fr");

                // Verify package reference with satellites gets created correctly in the .deps.json file
                VerifyDependency(dependencyContext, "Humanizer.Core", targetFramework == "net6.0" ? "lib/netstandard2.0/" : "lib/netstandard1.0/", "Humanizer",
                    "af", "ar", "az", "bg", "bn-BD", "cs", "da", "de", "el", "es", "fa", "fi-FI", "fr", "fr-BE", "he", "hr",
                    "hu", "hy", "id", "it", "ja", "lv", "ms-MY", "mt", "nb", "nb-NO", "nl", "pl", "pt", "ro", "ru", "sk", "sl", "sr",
                    "sr-Latn", "sv", "tr", "uk", "uz-Cyrl-UZ", "uz-Latn-UZ", "vi", "zh-CN", "zh-Hans", "zh-Hant");
            }

            var runtimeConfigJsonContents = File.ReadAllText(Path.Combine(publishDirectory.FullName, "TestApp.runtimeconfig.json"));
            var runtimeConfigJsonObject = JObject.Parse(runtimeConfigJsonContents);

            // Keep this list sorted
            var baselineConfigJsonObject = JObject.Parse(@"{
    ""runtimeOptions"": {
        ""configProperties"": {
            ""Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability"": true,
            ""System.AggressiveAttributeTrimming"": true,
            ""System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization"": false,
            ""System.Diagnostics.Debugger.IsSupported"": true,
            ""System.Diagnostics.Tracing.EventSource.IsSupported"": false,
            ""System.Globalization.Invariant"": true,
            ""System.Globalization.PredefinedCulturesOnly"": true,
            ""System.GC.Concurrent"": false,
            ""System.GC.Server"": true,
            ""System.GC.RetainVM"": false,
            ""System.Net.Http.EnableActivityPropagation"": false,
            ""System.Net.Http.UseNativeHttpHandler"": true,
            ""System.Reflection.Metadata.MetadataUpdater.IsSupported"": false,
            ""System.Reflection.NullabilityInfoContext.IsSupported"": false,
            ""System.Resources.ResourceManager.AllowCustomResourceTypes"": false,
            ""System.Resources.UseSystemResourceKeys"": true,
            ""System.Runtime.InteropServices.BuiltInComInterop.IsSupported"": false,
            ""System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting"": false,
            ""System.Runtime.InteropServices.EnableCppCLIHostActivation"": false,
            ""System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"": false,
            ""System.Runtime.TieredCompilation"": true,
            ""System.Runtime.TieredCompilation.QuickJit"": true,
            ""System.Runtime.TieredCompilation.QuickJitForLoops"": true,
            ""System.Runtime.TieredPGO"": true,
            ""System.StartupHookProvider.IsSupported"": false,
            ""System.Text.Encoding.EnableUnsafeUTF7Encoding"": false,
            ""System.Threading.Thread.EnableAutoreleasePool"": false,
            ""System.Threading.ThreadPool.MinThreads"": 2,
            ""System.Threading.ThreadPool.MaxThreads"": 9,
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
                targetFramework == "net6.0" ? "6.0.0" : "1.1.2";
            
            runtimeConfigJsonObject
                .Should()
                .BeEquivalentTo(baselineConfigJsonObject);
        }

        [Fact]
        public void It_fails_when_nobuild_is_set_and_build_was_not_performed_previously()
        {
            var publishCommand = GetPublishCommand(ToolsetInfo.CurrentTargetFramework).Execute("/p:NoBuild=true");
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
                    "net6.0",
                    new string[]
                    {
                        "TestApp.dll",
                        "TestApp.pdb",
                        "TestApp.deps.json",
                        "TestApp.runtimeconfig.json",
                        "TestLibrary.dll",
                        "TestLibrary.pdb",
                        "Newtonsoft.Json.dll",
                        "CompileCopyToOutput.cs",
                        "Resource1.resx",
                        "ContentAlways.txt",
                        "ContentPreserveNewest.txt",
                        "NoneCopyOutputAlways.txt",
                        "NoneCopyOutputPreserveNewest.txt",
                        "CopyToOutputFromProjectReference.txt",
                        "Humanizer.dll",
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
                        "az/Humanizer.resources.dll",
                        "hy/Humanizer.resources.dll",
                        "ms-MY/Humanizer.resources.dll",
                        "mt/Humanizer.resources.dll",
                        $"TestApp{EnvironmentInfo.ExecutableExtension}",
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
                        "System.Collections.NonGeneric.dll",
                        "System.Collections.Specialized.dll",
                        "System.ComponentModel.Primitives.dll",
                        "System.ComponentModel.TypeConverter.dll",
                        "System.Runtime.Serialization.Formatters.dll",
                        "System.Xml.XmlDocument.dll",
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
                        "af/Humanizer.resources.dll",
                        "az/Humanizer.resources.dll",
                        "hy/Humanizer.resources.dll",
                        "ms-MY/Humanizer.resources.dll",
                        "mt/Humanizer.resources.dll",
                    }
                };
            }
        }

    }
}
