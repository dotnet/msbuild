// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;
using Xunit.Abstractions;

using static Microsoft.NET.Build.Tasks.ResolveTargetingPackAssets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveTargetingPackAssetsTask : SdkTest
    {
        public GivenAResolveTargetingPackAssetsTask(ITestOutputHelper log)
            : base(log)
        {
        }

        [Fact]
        public void Given_ResolvedTargetingPacks_with_valid_PATH_in_PlatformManifest_It_resolves_TargetingPack()
        {
            ResolveTargetingPackAssets task = InitializeMockTargetingPackAssetsDirectory(out string mockPackageDirectory);

            task.Execute().Should().BeTrue();

            var reference = task.ReferencesToAdd[0];
            reference.ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "lib/Microsoft.Windows.SDK.NET.dll"));
            reference.GetMetadata("AssemblyName").Should().Be("Microsoft.Windows.SDK.NET");
            reference.GetMetadata("AssemblyVersion").Should().Be("10.0.18362.3");
            reference.GetMetadata("FileVersion").Should().Be("10.0.18362.3");
            reference.GetMetadata("PublicKeyToken").Should().Be("null");
            reference.GetMetadata("FrameworkReferenceName").Should().Be("Microsoft.Windows.SDK.NET.Ref");
            reference.GetMetadata("FrameworkReferenceVersion").Should().Be("5.0.0-preview1");

            task.PlatformManifests[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, $"data{Path.DirectorySeparatorChar}PlatformManifest.txt"));
            task.AnalyzersToAdd.Length.Should().Be(2);
            task.AnalyzersToAdd[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "analyzers/dotnet/anyAnalyzer.dll"));
            task.AnalyzersToAdd[1].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "analyzers/dotnet/cs/csAnalyzer.dll"));

            ((MockBuildEngine)task.BuildEngine).RegisteredTaskObjectsQueries.Should().Be(2,
                because: "There should be a lookup for the overall and the specific targeting pack");

            ((MockBuildEngine)task.BuildEngine).RegisteredTaskObjects.Count.Should().Be(2,
                because: "There should be a cache entry for the overall lookup and for the specific targeting pack");
        }

        [Fact]
        public void It_Uses_Multiple_Frameworks()
        {
            ResolveTargetingPackAssets task = InitializeMockTargetingPackAssetsDirectory(out string mockPackageDirectory);

            // Add two RuntimeFrameworks that both point to the default targeting pack.
            task.RuntimeFrameworks = new[] {
                new MockTaskItem("RuntimeFramework1", new Dictionary<string, string>{ ["FrameworkName"] = "Microsoft.Windows.SDK.NET.Ref"}),
                new MockTaskItem("RuntimeFramework2", new Dictionary<string, string>{ ["FrameworkName"] = "Microsoft.Windows.SDK.NET.Ref"}),
            };

            task.Execute().Should().BeTrue();

            task.UsedRuntimeFrameworks.Select(item => item.ItemSpec)
                .Should().BeEquivalentTo(new[]
                {
                    "RuntimeFramework1",
                    "RuntimeFramework2",
                });
        }

            [Fact]
        public void Given_Passing_ResolvedTargetingPacks_It_Passes_Again_With_Cached_Results()
        {
            ResolveTargetingPackAssets task1 = InitializeMockTargetingPackAssetsDirectory(out string packageDirectory);

            // Save off that build engine to inspect and reuse
            MockBuildEngine buildEngine = (MockBuildEngine)task1.BuildEngine;

            task1.Execute().Should().BeTrue();

            buildEngine.RegisteredTaskObjectsQueries.Should().Be(2,
                because: "there should be a lookup for the overall and the specific targeting pack");

            buildEngine.RegisteredTaskObjects.Count.Should().Be(2,
                because: "there should be a cache entry for the overall lookup and for the specific targeting pack");

            ResolveTargetingPackAssets task2 = InitializeTask(packageDirectory, buildEngine);

            task2.Execute().Should().BeTrue();

            buildEngine.RegisteredTaskObjectsQueries.Should().Be(3,
                because: "there should be a hit on the overall lookup this time through");

            buildEngine.RegisteredTaskObjects.Count.Should().Be(2,
                because: "the cache keys should match");
        }

        [Fact]
        public void Given_Passing_ResolvedTargetingPacks_A_Different_Language_Parses_Again()
        {
            ResolveTargetingPackAssets task1 = InitializeMockTargetingPackAssetsDirectory(out string packageDirectory);

            // Save off that build engine to inspect and reuse
            MockBuildEngine buildEngine = (MockBuildEngine)task1.BuildEngine;

            task1.Execute().Should().BeTrue();

            buildEngine.RegisteredTaskObjectsQueries.Should().Be(2,
                because: "there should be a lookup for the overall and the specific targeting pack");

            buildEngine.RegisteredTaskObjects.Count.Should().Be(2,
                because: "there should be a cache entry for the overall lookup and for the specific targeting pack");

            ResolveTargetingPackAssets task2 = InitializeTask(packageDirectory, buildEngine);

            task2.ProjectLanguage = "F#";

            task2.Execute().Should().BeTrue();

            buildEngine.RegisteredTaskObjectsQueries.Should().Be(4,
                because: "there should be no hits on the overall or targeting pack lookup this time through");

            buildEngine.RegisteredTaskObjects.Count.Should().Be(4,
                because: "there should be distinct results for C# and F#");
        }

        private ResolveTargetingPackAssets InitializeMockTargetingPackAssetsDirectory(out string mockPackageDirectory,
            [CallerMemberName] string testName = nameof(GivenAResolvePackageAssetsTask))
        {
            mockPackageDirectory = _testAssetsManager.CreateTestDirectory(testName: testName).Path;

            string dataDir = Path.Combine(mockPackageDirectory, "data");
            Directory.CreateDirectory(dataDir);

            File.WriteAllText(Path.Combine(dataDir, "FrameworkList.xml"), _frameworkList);
            File.WriteAllText(Path.Combine(dataDir, "PlatformManifest.txt"), "");

            return InitializeTask(mockPackageDirectory, new MockBuildEngine());
        }

        private ResolveTargetingPackAssets InitializeTask(string mockPackageDirectory, IBuildEngine buildEngine)
        {
            var task = new ResolveTargetingPackAssets()
            {
                BuildEngine = buildEngine,
            };

            task.FrameworkReferences = DefaultFrameworkReferences();

            task.ResolvedTargetingPacks = DefaultTargetingPacks(mockPackageDirectory);

            task.ProjectLanguage = "C#";

            return task;
        }

        private static MockTaskItem[] DefaultTargetingPacks(string mockPackageDirectory) => new[]
                    {
                new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                    new Dictionary<string, string>()
                    {
                        {MetadataKeys.NuGetPackageId, "Microsoft.Windows.SDK.NET.Ref"},
                        {MetadataKeys.NuGetPackageVersion, "5.0.0-preview1"},
                        {MetadataKeys.PackageConflictPreferredPackages, "Microsoft.Windows.SDK.NET.Ref;"},
                        {MetadataKeys.PackageDirectory, mockPackageDirectory},
                        {MetadataKeys.Path, mockPackageDirectory},
                        {"TargetFramework", "net5.0"}
                    })
            };
        private static MockTaskItem[] DefaultFrameworkReferences() => new[]
                    {
                new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
            };

        private readonly string _frameworkList =
@"<FileList Name=""cswinrt .NET Core 5.0"">
  <File Type=""Managed"" Path=""lib/Microsoft.Windows.SDK.NET.dll"" PublicKeyToken=""null"" AssemblyName=""Microsoft.Windows.SDK.NET"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Path=""analyzers/dotnet/anyAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""anyAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""cs"" Path=""analyzers/dotnet/cs/csAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""csAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""vb"" Path=""analyzers/dotnet/vb/vbAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""vbAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
</FileList>";


        [Fact]
        public void It_Hashes_All_Inputs()
        {
            IEnumerable<PropertyInfo> inputProperties;

            var task = InitializeTaskForHashTesting(out inputProperties);

            string oldHash;
            try
            {
                oldHash = task.GetInputs().CacheKey();
            }
            catch (ArgumentNullException)
            {
                Assert.True(
                    false,
                    nameof(StronglyTypedInputs) + " is likely not correctly handling null value of one or more optional task parameters");

                throw; // unreachable
            }

            foreach (var property in inputProperties)
            {
                switch (property.PropertyType)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(task, !(bool)property.GetValue(task));
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(task, property.Name);
                        break;

                    case var t when t == typeof(ITaskItem[]):
                        property.SetValue(task, new[] { new MockTaskItem() { ItemSpec = property.Name } });
                        // TODO: ideally this would also mutate the relevant metadata per item
                        break;

                    default:
                        Assert.True(false, $"{property.Name} is not a bool or string or ITaskItem[]. Update the test code to handle that.");
                        throw null; // unreachable
                }

                string newHash = task.GetInputs().CacheKey();
                newHash.Should().NotBe(
                    oldHash,
                    because: $"{property.Name} should be included in hash");

                oldHash = newHash;
            }
        }

        private ResolveTargetingPackAssets InitializeTaskForHashTesting(out IEnumerable<PropertyInfo> inputProperties)
        {
            inputProperties = typeof(ResolveTargetingPackAssets)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(p => !p.IsDefined(typeof(OutputAttribute)) &&
                            p.Name != nameof(ResolvePackageAssets.DesignTimeBuild))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            var requiredProperties = inputProperties
                .Where(p => p.IsDefined(typeof(RequiredAttribute)));

            ResolveTargetingPackAssets task = new();

            // Initialize all required properties as a genuine task invocation would. We do this
            // because HashSettings need not defend against required parameters being null.
            foreach (var property in requiredProperties)
            {
                property.PropertyType.Should().Be(
                    typeof(string),
                    because: $"this test hasn't been updated to handle non-string required task parameters like {property.Name}");

                property.SetValue(task, "_");
            }

            return task;
        }

        [Fact]
        public static void It_Hashes_All_Inputs_To_FrameworkList()
        {
            var constructor = typeof(FrameworkListDefinition).GetConstructors().Single();

            var parameters = constructor.GetParameters();

            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = parameters[i].ParameterType switch
                {
                    var t when t == typeof(string) => string.Empty,
                    _ => throw new NotImplementedException($"{parameters[i].ParameterType} is an unknown type. Update the test code to handle that.")
                };
            }

            FrameworkListDefinition defaultObject = (FrameworkListDefinition)constructor.Invoke(args);

            List<string> seenKeys = new List<string>(args.Length + 1);

            seenKeys.Add(defaultObject.CacheKey());

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = parameters[i].ParameterType switch
                {
                    var t when t == typeof(string) => "newValue",
                    var t when t == typeof(ITaskItem) => new MockTaskItem() { ItemSpec = "NewSpec" },
                    _ => throw new NotImplementedException($"{parameters[i].ParameterType} is an unknown type. Update the test code to handle that.")
                };

                string newKey = ((FrameworkListDefinition)constructor.Invoke(args)).CacheKey();

                seenKeys.Should().NotContain(newKey);

                seenKeys.Add(newKey);
            }
        }

        [Fact]
        public static void StronglyTypedInputs_Includes_All_Inputs_In_CacheKey()
        {
            StronglyTypedInputs defaultObject = new(
                frameworkReferences: DefaultFrameworkReferences(),
                resolvedTargetingPacks: DefaultTargetingPacks(Path.GetTempPath()),
                runtimeFrameworks: new[] {new MockTaskItem("RuntimeFramework1", new Dictionary<string, string>()) },
                generateErrorForMissingTargetingPacks: true,
                nuGetRestoreSupported: true,
                disableTransitiveFrameworkReferences: false,
            netCoreTargetingPackRoot: "netCoreTargetingPackRoot",
            projectLanguage: "C#");

            List<string> seenKeys = new();

            seenKeys.Add(defaultObject.CacheKey());

            foreach (var permutation in Permutations(defaultObject))
            {
                string newKey = permutation.Inputs.CacheKey();

                seenKeys.Should().NotContain(newKey,
                    because: $"The input {permutation.LastFieldChanged} should be included in the cache key");

                seenKeys.Add(newKey);
            }

            static IEnumerable<(string LastFieldChanged, StronglyTypedInputs Inputs)> Permutations(StronglyTypedInputs input)
            {
                var properties = typeof(StronglyTypedInputs).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    if (property.PropertyType == typeof(FrameworkReference[]))
                    {
                        var currentValue = (FrameworkReference[])property.GetValue(input);

                        foreach (var subfield in typeof(FrameworkReference).GetProperties())
                        {
                            if (subfield.PropertyType == typeof(string))
                            {
                                subfield.SetValue(currentValue[0], $"{subfield.Name}_changed");
                                yield return ($"{property.Name}.{subfield.Name}", input);
                                continue;
                            }

                            Assert.True(false, $"update test to understand fields of type {subfield.PropertyType} in {nameof(FrameworkReference)}");
                        }
                    }
                    else if (property.PropertyType == typeof(TargetingPack[]))
                    {
                        var currentValue = (TargetingPack[])property.GetValue(input);

                        foreach (var subproperty in typeof(TargetingPack).GetProperties())
                        {
                            if (subproperty.PropertyType == typeof(string))
                            {
                                subproperty.SetValue(currentValue[0], $"{subproperty.Name}_changed");
                                yield return ($"{property.Name}.{subproperty.Name}", input);
                                continue;
                            }

                            Assert.True(false, $"update test to understand fields of type {subproperty.PropertyType} in {nameof(TargetingPack)}");
                        }
                    }
                    else if (property.PropertyType == typeof(RuntimeFramework[]))
                    {
                        var currentValue = (RuntimeFramework[])property.GetValue(input);

                        foreach (var subproperty in typeof(RuntimeFramework).GetProperties())
                        {
                            if (subproperty.PropertyType == typeof(string))
                            {
                                subproperty.SetValue(currentValue[0], $"{subproperty.Name}_changed");
                                yield return ($"{property.Name}.{subproperty.Name}", input);
                                continue;
                            }

                            if (subproperty.PropertyType == typeof(ITaskItem))
                            {
                                // Used to store original items but not cache-relevant
                                continue;
                            }

                            Assert.True(false, $"update test to understand fields of type {subproperty.PropertyType} in {nameof(RuntimeFramework)}");
                        }
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(input, $"{property.Name}_changed");
                        yield return (property.Name, input);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        property.SetValue(input, !(bool)property.GetValue(input));
                        yield return (property.Name, input);
                    }
                    else
                    {
                        Assert.True(false, $"Unknown type {property.PropertyType} for field {property.Name}");
                    }
                }
            }
        }
    }
}

