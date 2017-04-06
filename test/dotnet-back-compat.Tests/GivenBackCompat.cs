// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenBackCompat : TestBase
    {
        [Theory]
        [InlineData("netcoreapp1.0", false, true)]
        [InlineData("netcoreapp1.1", false, true)]
        [InlineData("netstandard1.3", true, false)]
        [InlineData("netstandard1.6", true, false)]

        public void ItRestoresBuildsPacksRuns(string target, bool createNuGetPackage, bool executeProgram)
        {

            var testAppName = "TestAppSimple";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + "_" + target.Replace('.', '_'))
                .WithSourceFiles();

            //   Replace the 'TargetFramework'
            string projectFile = Path.Combine(testInstance.Root.ToString(), $"{testAppName}.csproj");
            var projectXml = XDocument.Load(projectFile);
            var ns = projectXml.Root.Name.Namespace;
            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();
            var rootNamespaceElement = propertyGroup.Element(ns + "TargetFramework");
            rootNamespaceElement.SetValue(target);
            projectXml.Save(projectFile);

            new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            if (createNuGetPackage)
            {
                new PackCommand()
                    .WithWorkingDirectory(testInstance.Root)
                    .Execute()
                    .Should().Pass();
            }

            if (executeProgram)
            {
                var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

                var outputDll = testInstance.Root.GetDirectory("bin", configuration, target)
                    .GetFile($"{testAppName}.dll");

                new DotnetCommand()
                    .ExecuteWithCapturedOutput(outputDll.FullName)
                    .Should().Pass()
                    .And.HaveStdOutContaining("Hello World");
            }
        }

    }
}
