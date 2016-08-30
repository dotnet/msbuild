// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using FluentAssertions.Json;
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
        [InlineData("dotnet.new", "1.0.0")]
        [InlineData("simple.dependencies", "1.0.0")]
        public void ItBuildsDependencyContextsFromProjectLockFiles(string mainProjectName, string mainProjectVersion)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(mainProjectName);

            DependencyContext dependencyContext = new DependencyContextBuilder().Build(
                mainProjectName,
                mainProjectVersion,
                compilerOptionsItem: null,
                lockFile: lockFile,
                framework: FrameworkConstants.CommonFrameworks.NetCoreApp10,
                runtime: null);

            JObject result = Save(dependencyContext);
            JObject baseline = ReadJson($"{mainProjectName}.deps.json");

            baseline
                .Should()
                .BeEquivalentTo(result);
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

        [Fact]
        public void ItBuildsDependencyContextsWithCompilerOptions()
        {
            LockFile lockFile = TestLockFiles.GetLockFile("dotnet.new");
            MockTaskItem compilerOptionsItem = new MockTaskItem(
                itemSpec: "CompilerOptions",
                metadata: new Dictionary<string, string>
                {
                    { "DefineConstants", "DEBUG;TRACE" },
                    { "LangVersion", "6" },
                    { "PlatformTarget", "x64" },
                    { "AllowUnsafeBlocks", "true" },
                    { "WarningsAsErrors", "false" },
                    //{ "Optimize", "" }, Explicitly not setting Optmize
                    { "AssemblyOriginatorKeyFile", "../keyfile.snk" },
                    { "DelaySign", "" },
                    { "PublicSign", "notFalseOrTrue" },
                    { "DebugType", "portable" },
                    { "OutputType", "Exe" },
                    { "GenerateDocumentationFile", "true" },
                });

            DependencyContext dependencyContext = new DependencyContextBuilder().Build(
                "dotnet.new",
                "1.0.0",
                compilerOptionsItem: compilerOptionsItem,
                lockFile: lockFile,
                framework: FrameworkConstants.CommonFrameworks.NetCoreApp10,
                runtime: null);

            dependencyContext.CompilationOptions.Defines.ShouldBeEquivalentTo(new object[] { "DEBUG", "TRACE" });
            dependencyContext.CompilationOptions.LanguageVersion.Should().Be("6");
            dependencyContext.CompilationOptions.Platform.Should().Be("x64");
            dependencyContext.CompilationOptions.AllowUnsafe.Should().Be(true);
            dependencyContext.CompilationOptions.WarningsAsErrors.Should().Be(false);
            dependencyContext.CompilationOptions.Optimize.Should().Be(null);
            dependencyContext.CompilationOptions.KeyFile.Should().Be("../keyfile.snk");
            dependencyContext.CompilationOptions.DelaySign.Should().Be(null);
            dependencyContext.CompilationOptions.PublicSign.Should().Be(null);
            dependencyContext.CompilationOptions.DebugType.Should().Be("portable");
            dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
            dependencyContext.CompilationOptions.GenerateXmlDocumentation.Should().Be(true);
        }
    }
}
