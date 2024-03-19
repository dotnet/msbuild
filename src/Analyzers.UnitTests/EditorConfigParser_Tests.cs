// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BuildCheck.Infrastructure.EditorConfig;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using static Microsoft.Build.BuildCheck.Infrastructure.EditorConfig.EditorConfigGlobsMatcher;


namespace Microsoft.Build.Analyzers.UnitTests
{
    public class EditorConfigParser_Tests
    {
        [Fact]
        public void NoSectionConfigured_ResultsEmptyResultConfig()
        {
            var configs = new List<EditorConfigFile>(){
                EditorConfigFile.Parse(""""
                    property1=value1
""""),
                EditorConfigFile.Parse(""""
                    property1=value2
                    """"),
                EditorConfigFile.Parse(""""
                    property1=value3
                    """"),
            };

            var parser = new EditorConfigParser();
            var mergedResult = parser.MergeEditorConfigFiles(configs, "/some/path/to/file");
            mergedResult.Keys.Count.ShouldBe(0);
        }

        [Fact]
        public void ProperOrderOfconfiguration_ClosestToTheFileShouldBeApplied()
        {
            var configs = new List<EditorConfigFile>(){
                EditorConfigFile.Parse(""""
                    [*]
                    property1=value1
""""),
                EditorConfigFile.Parse(""""
                    [*]
                    property1=value2
                    """"),
                EditorConfigFile.Parse(""""
                    [*]
                    property1=value3
                    """"),
            };

            var parser = new EditorConfigParser();
            var mergedResult = parser.MergeEditorConfigFiles(configs, "/some/path/to/file.proj");
            mergedResult.Keys.Count.ShouldBe(1);
            mergedResult["property1"].ShouldBe("value1");
        }

        [Fact]
        public void EditorconfigFileDiscovery_RootTrue()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();

            TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
            TransientTestFolder workFolder2 = testEnvironment.CreateFolder(Path.Combine(workFolder1.Path, "subfolder"), createFolder: true);

            TransientTestFile config1 = testEnvironment.CreateFile(workFolder2, ".editorconfig",
            """
            root=true

            [*.csproj]
            test_key=test_value_updated
            """);


            TransientTestFile config2 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
            """
            [*.csproj]
            test_key=should_not_be_respected_and_parsed
            """);

            var parser = new EditorConfigParser();
            var listOfEditorConfigFile = parser.EditorConfigFileDiscovery(Path.Combine(workFolder1.Path, "subfolder", "projectfile.proj") ).ToList();
            // should be one because root=true so we do not need to go further
            listOfEditorConfigFile.Count.ShouldBe(1);
            listOfEditorConfigFile[0].IsRoot.ShouldBeTrue();
            listOfEditorConfigFile[0].NamedSections[0].Name.ShouldBe("*.csproj");
            listOfEditorConfigFile[0].NamedSections[0].Properties["test_key"].ShouldBe("test_value_updated");
        }

        [Fact]
        public void EditorconfigFileDiscovery_RootFalse()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();

            TransientTestFolder workFolder1 = testEnvironment.CreateFolder(createFolder: true);
            TransientTestFolder workFolder2 = testEnvironment.CreateFolder(Path.Combine(workFolder1.Path, "subfolder"), createFolder: true);

            TransientTestFile config1 = testEnvironment.CreateFile(workFolder2, ".editorconfig",
            """
            [*.csproj]
            test_key=test_value_updated
            """);

            TransientTestFile config2 = testEnvironment.CreateFile(workFolder1, ".editorconfig",
            """
            [*.csproj]
            test_key=will_be_there
            """);

            var parser = new EditorConfigParser();
            var listOfEditorConfigFile = parser.EditorConfigFileDiscovery(Path.Combine(workFolder1.Path, "subfolder", "projectfile.proj")).ToList();

            listOfEditorConfigFile.Count.ShouldBe(2);
            listOfEditorConfigFile[0].IsRoot.ShouldBeFalse();
            listOfEditorConfigFile[0].NamedSections[0].Name.ShouldBe("*.csproj");
        }
    }
}
