// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ImportCollection_Tests
    {
        [Test]
        public void TestICollectionMethods()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", @"

                    <Project xmlns=`msbuildnamespace`>

                        <Import Project=`import1.proj` />
                        <Import Project=`import2.proj` />

                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>

                    </Project>

                ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);

            string import1Path = Path.Combine(ObjectModelHelpers.TempProjectDir, "import1.proj");
            string import2Path = Path.Combine(ObjectModelHelpers.TempProjectDir, "import2.proj");

            ImportCollection imports = project.Imports;

            Assertion.AssertEquals(2, imports.Count);

            Import[] array = new Import[2];
            imports.CopyTo(array, 0);
            Dictionary<string, Import> hash = new Dictionary<string, Import>(StringComparer.OrdinalIgnoreCase);
            hash[array[0].EvaluatedProjectPath] = array[0];
            hash[array[1].EvaluatedProjectPath] = array[1];

            Assertion.AssertEquals(imports[import1Path], hash[import1Path]);
            Assertion.AssertEquals(imports[import2Path], hash[import2Path]);

            object[] arrayObjects = new object[2];
            imports.CopyTo(arrayObjects, 0);
            hash.Clear();
            hash[((Import)arrayObjects[0]).EvaluatedProjectPath] = ((Import)arrayObjects[0]);
            hash[((Import)arrayObjects[1]).EvaluatedProjectPath] = ((Import)arrayObjects[1]);

            Assertion.AssertEquals(imports[import1Path], hash[import1Path]);
            Assertion.AssertEquals(imports[import2Path], hash[import2Path]);

            Assertion.AssertEquals("import1.proj", imports[import1Path].ProjectPath);
            Assertion.AssertEquals("import2.proj", imports[import2Path].ProjectPath);
        }

        [Test]
        public void RemoveExistingImport()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", @"

                    <Project xmlns=`msbuildnamespace`>

                        <Import Project=`import1.proj` />
                        <Import Project=`import2.proj` />

                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>

                    </Project>

                ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
            
            ImportCollection imports = project.Imports;
            Assertion.AssertEquals(2, imports.Count);
            
            imports.RemoveImport(imports[Path.Combine(ObjectModelHelpers.TempProjectDir, "import1.proj")]);
            
            // First validate that the ImportCollection only contains a single Import
            Assertion.AssertEquals(1, imports.Count);

            // Now validate that the ImportCollection properly updated its parent Project (by inspecting the 
            // project's in-memory Xml)
            int importCount = 0;
            
            foreach (XmlNode childNode in project.ProjectElement)
            {
                if (childNode.Name == XMakeElements.import)
                {
                    importCount++;
                }
            }

            Assertion.AssertEquals(1, importCount);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AttemptToRemoveImportedImportShouldThrowException()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                        <Import Project=`import3.proj` />
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import3.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", @"

                    <Project xmlns=`msbuildnamespace`>

                        <Import Project=`import1.proj` />
                        <Import Project=`import2.proj` />

                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>

                    </Project>

                ");

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);

            ImportCollection imports = project.Imports;
            
            // Should throw an InvalidOperationException
            imports.RemoveImport(imports[Path.Combine(ObjectModelHelpers.TempProjectDir, "import3.proj")]);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AttemptToRemoveImportFromAnotherProjectShouldThrowException()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", @"
                    <Project xmlns=`msbuildnamespace`>
                    </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("main1.proj", @"

                    <Project xmlns=`msbuildnamespace`>

                        <Import Project=`import1.proj` />
            
                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>

                    </Project>

                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("main2.proj", @"

                    <Project xmlns=`msbuildnamespace`>

                        <Import Project=`import1.proj` />
            
                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>

                    </Project>

                ");

            Project project1 = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main1.proj", null);
            Project project2 = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main2.proj", null);

            ImportCollection imports1 = project1.Imports;
            ImportCollection imports2 = project2.Imports;

            // Should throw an InvalidOperationException
            imports1.RemoveImport(imports2[Path.Combine(ObjectModelHelpers.TempProjectDir, "import1.proj")]);
        }
    }
}
