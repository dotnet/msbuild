// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class GivenADependencyContextBuilder
    {
        /// <summary>
        /// Tests that DependencyContextBuilder generates DependencyContexts correctly.
        /// </summary>
        [Theory]
        [MemberData("ProjectData")]
        public void ItBuildsDependencyContextsFromProjectLockFiles(
            string mainProjectName,
            string mainProjectVersion,
            CompilationOptions compilationOptions,
            string baselineFileName,
            string runtime)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            DependencyContext dependencyContext = new DependencyContextBuilder().Build(
                mainProjectName,
                mainProjectVersion,
                compilationOptions,
                lockFile,
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                runtime);

            JObject result = Save(dependencyContext);
            JObject baseline = ReadJson($"{baselineFileName}.deps.json");

            try
            {
                baseline
                    .Should()
                    .BeEquivalentTo(result);
            }
            catch
            {
                // write the result file out on failure for easy comparison

                using (JsonTextWriter writer = new JsonTextWriter(File.CreateText($"result-{baselineFileName}.deps.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(writer, result);
                }

                throw;
            }
        }

        public static IEnumerable<object[]> ProjectData
        {
            get
            {
                CompilationOptions compilationOptions = new CompilationOptions(
                    defines: new[] { "DEBUG", "TRACE" },
                    languageVersion: "6",
                    platform: "x64",
                    allowUnsafe: true,
                    warningsAsErrors: false,
                    optimize: null,
                    keyFile: "../keyfile.snk",
                    delaySign: null,
                    publicSign: null,
                    debugType: "portable",
                    emitEntryPoint: true,
                    generateXmlDocumentation: true);

                return new[]
                {
                    new object[] { "dotnet.new", "1.0.0", null, "dotnet.new", null },
                    new object[] { "simple.dependencies", "1.0.0", null, "simple.dependencies", null },
                    new object[] { "simple.dependencies", "1.0.0", compilationOptions, "simple.dependencies.compilerOptions", null },
                    new object[] { "all.asset.types", "1.0.0", null, "all.asset.types.portable", null },
                    new object[] { "all.asset.types", "1.0.0", null, "all.asset.types.osx", "osx.10.11-x64" },
                };
            }
        }

        private static JObject ReadJson(string path)
        {
            using (JsonTextReader jsonReader = new JsonTextReader(File.OpenText(path)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(jsonReader);
            }
        }

        private JObject Save(DependencyContext dependencyContext)
        {
            using (var memoryStream = new MemoryStream())
            {
                new DependencyContextWriter().Write(dependencyContext, memoryStream);
                using (var readStream = new MemoryStream(memoryStream.ToArray()))
                {
                    using (var textReader = new StreamReader(readStream))
                    {
                        using (var reader = new JsonTextReader(textReader))
                        {
                            return JObject.Load(reader);
                        }
                    }
                }
            }
        }
    }
}
