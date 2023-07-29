// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACompilationOptionsConverter
    {
        private static MethodInfo s_convertFromMethod = typeof(GenerateDepsFile)
            .GetTypeInfo()
            .Assembly
            .GetType("Microsoft.NET.Build.Tasks.CompilationOptionsConverter")
            .GetMethod("ConvertFrom");
        
        [Theory]
        [MemberData(nameof(CompilerOptionsData))]
        public void ItConvertsFromITaskItemsCorrectly(ITaskItem taskItem, CompilationOptions expectedOptions)
        {
            CompilationOptions resultOptions = (CompilationOptions)s_convertFromMethod.Invoke(null, new object[] {taskItem});

            resultOptions.Should().BeEquivalentTo(expectedOptions);
        }

        public static IEnumerable<object[]> CompilerOptionsData
        {
            get
            {
                yield return new object[] {
                    new MockTaskItem(
                        itemSpec: "CompilerOptions",
                        metadata: new Dictionary<string, string>
                        {
                            { "DefineConstants", "RELEASE;NETCOREAPP1_0" },
                            { "LangVersion", "6" },
                            { "PlatformTarget", "x64" },
                            { "AllowUnsafeBlocks", "true" },
                            { "TreatWarningsAsErrors", "false" },
                            //{ "Optimize", "" }, Explicitly not setting Optimize
                            { "AssemblyOriginatorKeyFile", "../keyfile.snk" },
                            { "DelaySign", "" },
                            { "PublicSign", "notFalseOrTrue" },
                            { "DebugType", "portable" },
                            { "OutputType", "Exe" },
                            { "GenerateDocumentationFile", "true" },
                        }
                    ),
                    new CompilationOptions(
                        defines: new[] { "RELEASE", "NETCOREAPP1_0" },
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
                        generateXmlDocumentation: true)
                };

                yield return new object[] {
                    new MockTaskItem(
                        itemSpec: "CompilerOptions",
                        metadata: new Dictionary<string, string>
                        {
                            { "DefineConstants", ";NETCOREAPP1_0" },
                            { "LangVersion", "6" },
                            { "PlatformTarget", "x64" },
                            { "AllowUnsafeBlocks", "true" },
                            { "TreatWarningsAsErrors", "false" },
                            //{ "Optimize", "" }, Explicitly not setting Optimize
                            { "AssemblyOriginatorKeyFile", "../keyfile.snk" },
                            { "DelaySign", "" },
                            { "PublicSign", "notFalseOrTrue" },
                            { "DebugType", "portable" },
                            { "OutputType", "Exe" },
                            { "GenerateDocumentationFile", "true" },
                        }
                    ),
                    new CompilationOptions(
                        defines: new[] { "NETCOREAPP1_0" },
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
                        generateXmlDocumentation: true)
                };

                yield return new object[]
                {
                    null,
                    null
                };
            }
        }
    }
}
