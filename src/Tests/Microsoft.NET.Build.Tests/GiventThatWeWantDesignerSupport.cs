// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantDesignerSupport : SdkTest
    {
        public GivenThatWeWantDesignerSupport(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net46", "false")]
        [InlineData("netcoreapp3.0", "true")]
        [InlineData("netcoreapp3.0", "false")]
        [InlineData("net5.0-windows", "true")]
        [InlineData("net5.0-windows", "false")]
        [InlineData("net7.0-windows10.0.17763", "true")]
        [InlineData("net7.0-windows10.0.17763", "false")]
        public void It_provides_runtime_configuration_and_shadow_copy_files_via_outputgroup(string targetFramework, string isSelfContained)
        {
            if ((targetFramework == "net5.0-windows" || targetFramework == "net7.0-windows10.0.17763")
                && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // net5.0-windows is windows only scenario
                return;
            }

            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = targetFramework,
            };

            var project = new TestProject
            {
                Name = "DesignerTest",
                IsExe = true,
                TargetFrameworks = targetFramework,
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) },
                ReferencedProjects = { projectRef },
                SelfContained = isSelfContained
            };

            var asset = _testAssetsManager
                .CreateTestProject(project, identifier: targetFramework);

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, project.Name),
                targetFramework,
                "DesignerRuntimeImplementationProjectOutputGroupOutput",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "DesignerRuntimeImplementationProjectOutputGroup",
                MetadataNames = { "TargetPath" },
            };

            command.Execute().Should().Pass();

            var items =
                from item in command.GetValuesWithMetadata()
                select new
                {
                    Identity = item.value,
                    TargetPath = item.metadata["TargetPath"]
                };

            string depsFile = null;
            string runtimeConfig = null;
            var otherFiles = new List<string>();

            foreach (var item in items)
            {
                Path.IsPathFullyQualified(item.Identity).Should().BeTrue();
                Path.GetFileName(item.Identity).Should().Be(item.TargetPath);

                switch (item.TargetPath)
                {
                    case "DesignerTest.designer.deps.json":
                        depsFile = item.Identity;
                        break;
                    case "DesignerTest.designer.runtimeconfig.json":
                        runtimeConfig = item.Identity;
                        break;
                    default:
                        otherFiles.Add(item.TargetPath);
                        break;
                }
            }

            switch (targetFramework)
            {
                case "netcoreapp3.0":
                case "net5.0-windows":
                case "net7.0-windows10.0.17763":
                    var depsFileLibraries = GetRuntimeLibraryFileNames(depsFile);
                    depsFileLibraries.Should().BeEquivalentTo(new[] { "Newtonsoft.Json.dll" });

                    var options = GetRuntimeOptions(runtimeConfig);
                    options["configProperties"]["Microsoft.NETCore.DotNetHostPolicy.SetAppPaths"].Value<bool>().Should().BeTrue();
                    // runtimeconfiguration should not have platform.
                    // it should be net5.0 instead of net5.0-windows
                    options["tfm"].Value<string>().Should().Be(targetFramework.Split('-')[0]);
                    options["additionalProbingPaths"].Value<JArray>().Should().NotBeEmpty();

                    if (targetFramework == "net7.0-windows10.0.17763")
                    {
                        otherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb", "Microsoft.Windows.SDK.NET.dll", "WinRT.Runtime.dll"]);
                    }
                    else
                    {
                        otherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb"]);
                    }

                    break;

                case "net46":
                    depsFile.Should().BeNull();
                    runtimeConfig.Should().BeNull();
                    otherFiles.Should().BeEquivalentTo(["Newtonsoft.Json.dll", "ReferencedProject.dll", "ReferencedProject.pdb"]);
                    break;
            }
        }

        private static JToken GetRuntimeOptions(string runtimeConfigFilePath)
        {
            var config = ParseRuntimeConfig(runtimeConfigFilePath);
            return config["runtimeOptions"];
        }

        private static IEnumerable<string> GetRuntimeLibraryFileNames(string depsFilePath)
        {
            var deps = ParseDepsFile(depsFilePath);

            return deps.RuntimeLibraries
                       .SelectMany(r => r.RuntimeAssemblyGroups)
                       .SelectMany(a => a.AssetPaths)
                       .Select(p => Path.GetFileName(p));
        }

        private static JToken ParseRuntimeConfig(string path)
        {
            using (var streamReader = File.OpenText(path))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return JObject.Load(jsonReader);
            }
        }

        private static DependencyContext ParseDepsFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new DependencyContextJsonReader())
            {
                return reader.Read(stream);
            }
        }
    }
}
