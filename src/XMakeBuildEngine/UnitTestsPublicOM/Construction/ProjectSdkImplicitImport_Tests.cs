// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectImportElement class when imports are implicit through an Sdk specification.
    /// </summary>
    public class ProjectSdkImplicitImport_Tests
    {
        [Fact]
        public void SdkImportsAreInImportList()
        {
            var testSdkRoot = Path.Combine(Path.GetTempPath(), "MSBuildUnitTest");
            var testSdkDirectory = Path.Combine(testSdkRoot, "MSBuildUnitTestSdk", "Sdk");

            try
            {
                Directory.CreateDirectory(testSdkDirectory);

                string sdkPropsPath = Path.Combine(testSdkDirectory, "Sdk.props");
                string sdkTargetsPath = Path.Combine(testSdkDirectory, "Sdk.targets");

                File.WriteAllText(sdkPropsPath, "<Project />");
                File.WriteAllText(sdkTargetsPath, "<Project />");

                using (new Helpers.TemporaryEnvironment(MSBuildConstants.SdksPath, testSdkRoot))
                {
                    string content = @"
                    <Project Sdk='MSBuildUnitTestSdk' >
                    </Project>";

                    ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

                    List<ProjectImportElement> imports = Helpers.MakeList(project.Imports);

                    Assert.Equal(2, imports.Count);
                    Assert.Equal(sdkPropsPath, imports[0].Project);
                    Assert.True(imports[0].IsImplicit);
                    Assert.Equal(sdkTargetsPath, imports[1].Project);
                    Assert.True(imports[1].IsImplicit);
                }
            }
            finally
            {
                if (Directory.Exists(testSdkRoot))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testSdkDirectory, true);
                }
            }
        }


    }
}
