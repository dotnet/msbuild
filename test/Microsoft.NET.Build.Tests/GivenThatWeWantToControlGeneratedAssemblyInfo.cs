// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToControlGeneratedAssemblyInfo : SdkTest
    {
        //[Theory]
        //[InlineData("AssemblyInformationVersionAttribute")]
        //[InlineData("AssemblyFileVersionAttribute")]
        //[InlineData("AssemblyVersionAttribute")]
        //[InlineData("AssemblyCompanyAttribute")]
        //[InlineData("AssemblyConfigurationAttribute")]
        //[InlineData("AssemblyCopyrightAttribute")]
        //[InlineData("AssemblyDescriptionAttribute")]
        //[InlineData("AssemblyTitleAttribute")]
        //[InlineData("NeutralResourcesLanguageAttribute")]
        //[InlineData("All")]
        public void It_respects_opt_outs(string attributeToOptOut)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: Path.DirectorySeparatorChar + attributeToOptOut)
                .WithSource()
                .Restore();

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);
            buildCommand
                .Execute(
                    "/p:Version=1.2.3-beta",
                    "/p:FileVersion=4.5.6.7",
                    "/p:AssemblyVersion=8.9.10.11",
                    "/p:Company=TestCompany",
                    "/p:Configuration=Release",
                    "/p:Copyright=TestCopyright",
                    "/p:Description=TestDescription",
                    "/p:Product=TestProduct",
                    "/p:AssemblyTitle=TestTitle",
                    "/p:NeutralLanguage=fr",
                    attributeToOptOut == "All" ?
                        "/p:GenerateAssemblyInfo=false" :
                        $"/p:Generate{attributeToOptOut}=false"
                    )
                .Should()
                .Pass();

            var expectedInfo = new SortedDictionary<string, string>
            {
                { "AssemblyInformationalVersionAttribute", "1.2.3-beta" },
                { "AssemblyFileVersionAttribute", "4.5.6.7" },
                { "AssemblyVersionAttribute", "8.9.10.11" },
                { "AssemblyCompanyAttribute", "TestCompany" },
                { "AssemblyConfigurationAttribute", "Release" },
                { "AssemblyCopyrightAttribute", "TestCopyright" },
                { "AssemblyDescriptionAttribute", "TestDescription" },
                { "AssemblyProductAttribute", "TestProduct" },
                { "AssemblyTitleAttribute", "TestTitle" },
                { "NeutralResourcesLanguageAttribute", "fr" },
            };

            if (attributeToOptOut == "All")
            {
                expectedInfo.Clear();
            }
            else
            {
                expectedInfo.Remove(attributeToOptOut);
            }

            expectedInfo.Add("TargetFrameworkAttribute", ".NETCoreApp,Version=v1.0");

            var assemblyPath = Path.Combine(buildCommand.GetOutputDirectory("netcoreapp1.0", "Release").FullName, "HelloWorld.dll");
            var actualInfo = AssemblyInfo.Get(assemblyPath);

            actualInfo.Should().Equal(expectedInfo);
        }
    }
}
