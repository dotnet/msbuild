// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class Item_Tests
    {
        /// <summary>
        /// Both Include and Exclude should be unescaped before one is subtracted from the other
        /// </summary>
        [Test]
        public void IncludeAndExcludeUnescaping()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "IncludeAndExcludeUnescaping");
            string matchesFoo = Path.Combine(tempFolder, "*");
            string foo = Path.Combine(tempFolder, "foo");
            StreamWriter sw = null;

            try
            {
                Directory.CreateDirectory(tempFolder);
                sw = File.CreateText(foo);

                string projectContents = String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <ItemGroup>
                            <i1 Include=`)` Exclude=`)`/>
                            <i2 Include=`%29` Exclude=`%29`/>
                            <i3 Include=`%29` Exclude=`)`/>
                            <i4 Include=`)` Exclude=`%29`/>
                            <i5 Include=`);x` Exclude=`%29`/>
                            <i6 Include=`x` Exclude=`y`/>
                            <i7 Include=` ` Exclude=`y`/>
                            <i8 Include=`x` Exclude=``/>
                            <i9 Include=`);%29` Exclude=`)`/>
                            <i10 Include=`%2a` Exclude=`%2a`/>
                            <i11 Include=`{0}` Exclude=`{1}`/>
                            <i12 Include=`{1}` Exclude=`{1}`/>
                        </ItemGroup>
                    </Project>
                    ", foo, matchesFoo);

                Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

                BuildItemGroup i1Items = project.GetEvaluatedItemsByName("i1");
                BuildItemGroup i2Items = project.GetEvaluatedItemsByName("i2");
                BuildItemGroup i3Items = project.GetEvaluatedItemsByName("i3");
                BuildItemGroup i4Items = project.GetEvaluatedItemsByName("i4");
                BuildItemGroup i5Items = project.GetEvaluatedItemsByName("i5");
                BuildItemGroup i6Items = project.GetEvaluatedItemsByName("i6");
                BuildItemGroup i7Items = project.GetEvaluatedItemsByName("i7");
                BuildItemGroup i8Items = project.GetEvaluatedItemsByName("i8");
                BuildItemGroup i9Items = project.GetEvaluatedItemsByName("i9");
                BuildItemGroup i10Items = project.GetEvaluatedItemsByName("i10");
                BuildItemGroup i11Items = project.GetEvaluatedItemsByName("i11");
                BuildItemGroup i12Items = project.GetEvaluatedItemsByName("i12");
                Assertion.Assert(") should exclude )", i1Items.Count == 0);
                Assertion.Assert("%29 should exclude %29", i2Items.Count == 0);
                Assertion.Assert(") should exclude %29", i3Items.Count == 0);
                Assertion.Assert("%29 should exclude )", i4Items.Count == 0);
                Assertion.Assert("%29 should exclude ) from );x", i5Items.Count == 1 && i5Items[0].FinalItemSpecEscaped == "x");
                Assertion.Assert("y should not exclude x", i6Items.Count == 1 && i6Items[0].FinalItemSpecEscaped == "x");
                Assertion.Assert("empty include, y exclude", i7Items.Count == 0);
                Assertion.Assert("x include, empty exclude", i8Items.Count == 1 && i8Items[0].FinalItemSpecEscaped == "x");
                Assertion.Assert(") should exclude both from );%29", i9Items.Count == 0);
                Assertion.Assert("%2a should exclude %2a", i10Items.Count == 0);
                Assertion.Assert("* matching foo should exclude foo", i11Items.Count == 0);
                Assertion.Assert("* should exclude *", i12Items.Count == 0); 
            }
            finally
            {
                if (null != sw) sw.Close();
                File.Delete(foo);
                Directory.Delete(tempFolder);
            }
        }

        [Test]
        public void ItemMetadataShouldBeEvaluatedEarly()
        {
            string projectOriginalContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <CultureItem Include=`spanish`/>
                        <Compile Include=`a`>
                            <Culture>@(CultureItem)</Culture>
                        </Compile>
                        <CultureItem Include=`french`/>
                    </ItemGroup>
                
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectOriginalContents);

            BuildItemGroup compileItems = project.GetEvaluatedItemsByName("Compile");
            string evaluatedCulture = compileItems[0].GetEvaluatedMetadata("Culture");

            Assertion.AssertEquals("Culture should be 'spanish'", "spanish", evaluatedCulture);
        }

        [Test]
        public void HasMetadataAndGetItemMetadata()
        {
            string projectContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <i Include=`i1`/>
                        <i Include=`i2`>
                            <Culture>klingon</Culture>
                        </i>
                        <i Include=`i3`>
                            <Culture/>
                        </i>
                        <i Include=`i4`>
                            <Culture></Culture>
                        </i>
                        <i Include=`i1`>
                            <Culture>vulcan</Culture>
                        </i>
                    </ItemGroup>

                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildItemGroup iItems = project.GetEvaluatedItemsByName("i");
            // I believe it is safe to assume ordering as ItemGroup is backed up by an ArrayList
            Assertion.Assert(iItems[0].FinalItemSpecEscaped == "i1");
            Assertion.Assert(!iItems[0].HasMetadata("Culture")); // Does not have it 
            Assertion.Assert(iItems[1].FinalItemSpecEscaped == "i2");
            Assertion.Assert(iItems[1].HasMetadata("Culture"));
            Assertion.Assert(iItems[1].HasMetadata("CuLtUrE"));
            Assertion.Assert(iItems[1].GetMetadata("CuLtUrE") == "klingon");
            Assertion.Assert(iItems[2].FinalItemSpecEscaped == "i3");
            Assertion.Assert(iItems[2].HasMetadata("Culture"));
            Assertion.Assert(iItems[2].GetMetadata("Culture") == ""); 
            Assertion.Assert(iItems[3].FinalItemSpecEscaped == "i4");
            Assertion.Assert(iItems[3].HasMetadata("Culture"));
            Assertion.Assert(iItems[3].GetMetadata("Culture") == ""); 
            Assertion.Assert(iItems[4].FinalItemSpecEscaped == "i1");
            Assertion.Assert(iItems[4].HasMetadata("Culture")); 
            Assertion.Assert(iItems[4].GetMetadata("Culture") == "vulcan"); 
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void HasMetadataPassNull()
        {
            string projectContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <i Include=`i1`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildItemGroup iItems = project.GetEvaluatedItemsByName("i");
            iItems[0].HasMetadata(null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void HasMetadataPassEmpty()
        {
            string projectContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <i Include=`i1`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildItemGroup iItems = project.GetEvaluatedItemsByName("i");
            iItems[0].HasMetadata("");
        }

        [Test]
        public void HasMetadataBuiltInMetadata()
        {
            string projectContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <i Include=`i1`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildItemGroup iItems = project.GetEvaluatedItemsByName("i");
            Assertion.Assert(iItems[0].HasMetadata("Filename"));
            Assertion.Assert("i1" == iItems[0].GetMetadata("Filename"));
        }

        [Test]
        public void HasMetadataInvalidName()
        {
            string projectContents = @"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <i Include=`i1`/>
                    </ItemGroup>
                </Project>
                ";

            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildItemGroup iItems = project.GetEvaluatedItemsByName("i");
            // Shouldn't check whether the attribute /could/ exist (it couldn't)
            // just return that it doesn't.
            Assertion.Assert(!iItems[0].HasMetadata("_!@#$%^&*()"));
        }

        /// <summary>
        /// Verify we can't create items with invalid names in projects
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameInProject()
        {
            bool fExceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <ItemGroup Condition=\"false\"><Choose Include=\"blah\"/></ItemGroup>"
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameDirectPrivateCreate()
        {
            bool fExceptionCaught = false;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<Choose Include=\"blah\"/>");
                XmlElement element = doc.DocumentElement;
                BuildItem item = new BuildItem(doc.DocumentElement, false, new ItemDefinitionLibrary(new Project()));
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameDirectPublicCreate()
        {
            bool fExceptionCaught = false;
            try
            {
                BuildItem item = new BuildItem("Choose", "blah");
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameDirectPublicCreate2()
        {
            bool fExceptionCaught = false;
            try
            {
                BuildItem item = new BuildItem("Choose", new TaskItem("blah"));
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameDirectPrivateCreate2()
        {
            bool fExceptionCaught = false;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<Choose/>");
                BuildItem item = new BuildItem(doc, "Choose", "value", new ItemDefinitionLibrary(new Project()));
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names by renaming a valid item
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemNameRename()
        {
            bool fExceptionCaught = false;
            BuildItem item = new BuildItem("my", "precioussss");
            try
            {
                item.Name = "Choose";
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid metadata names in projects
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemMetadataNameInProject()
        {
            bool fExceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <ItemGroup Condition=\"false\"><Chooses Include=\"blah\"><When/></Chooses></ItemGroup>"
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create items with invalid names by renaming a valid item
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidItemMetadataNameRename()
        {
            bool fExceptionCaught = false;
            BuildItem item = new BuildItem("my", "precioussss");
            try
            {
                item.SetMetadata("Choose", "blah");
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }


        /// <summary>
        /// Verify invalid item names are caught, where the names are valid Xml Element names.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void InvalidCharInItemNameInProject()
        {
            bool exceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <ItemGroup Condition=\"false\"><\u03A3/></ItemGroup>" // \u03A3 == sigma
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                exceptionCaught = true;
            }
            Assertion.Assert(exceptionCaught);
        }

        /// <summary>
        /// Verify invalid metadata names are caught, where the names are valid Xml Element names.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void InvalidCharInMetadataNameInProject()
        {
            bool exceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <ItemGroup Condition=\"false\"><X Include=\"y\"><Meta\u03A3data/></X></ItemGroup>" // \u03A3 == sigma
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                exceptionCaught = true;
            }
            Assertion.Assert(exceptionCaught);
        }

        internal static string[] validItemPropertyMetadataNames = new string[]
        {
            "x",
            "x1",
            "_x",
            "_1-",
            "aa1"
        };

        internal static string[] invalidItemPropertyMetadataNames = new string[]
        {
            " ",
            "$",
            "@",
            "(",
            ")",
            "%",
            "*",
            "?",
            ".",
            " ",
            "1",
            "\n",
            ":",
            "xxx$",
            "xxx@",
            "xxx(",
            "xxx)",
            "xxx%",
            "xxx*",
            "xxx?",
            "xxx.",
            "xxx:",
            "\u03A3",            // sigma is a valid XML element name, but invalid item/property name
            "a\u03A3",
            "aa\u03A3"
        };

        /// <summary>
        /// Check the valid names are accepted
        /// </summary>
        [Test]
        public void ValidName()
        {
            foreach (string candidate in validItemPropertyMetadataNames)
            {
                TryValidItemName(candidate);
            }
        }

        /// <summary>
        /// Check the invalid names are rejected
        /// </summary>
        [Test]
        public void InvalidNames()
        {
            foreach (string candidate in invalidItemPropertyMetadataNames)
            {
                TryInvalidItemName(candidate);
            }
        }

        /// <summary>
        /// Helper for trying invalid item names
        /// </summary>
        /// <param name="name"></param>
        private void TryInvalidItemName(string name)
        {
            XmlDocument doc = new XmlDocument();
            bool caughtException = false;

            // Test the BuildItem ctor
            try
            {
                BuildItem item = new BuildItem(doc, name, "someItemSpec", new ItemDefinitionLibrary(new Project()));
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                caughtException = true;
            }
            Assertion.Assert(name, caughtException);

            // Test the Name setter codepath
            caughtException = false;
            try
            {
                BuildItem item = new BuildItem(doc, "someName", "someItemSpec", new ItemDefinitionLibrary(new Project()));
                item.Name = name;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                caughtException = true;
            }
            Assertion.Assert(name, caughtException);

            // Test the metadata setter codepath
            caughtException = false;
            try
            {
                BuildItem item = new BuildItem(doc, "someName", "someItemSpec", new ItemDefinitionLibrary(new Project()));
                item.SetMetadata(name, "someValue");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                caughtException = true;
            }
            Assertion.Assert(name, caughtException);
        }

        /// <summary>
        /// Helper for trying valid item names
        /// </summary>
        /// <param name="name"></param>
        private void TryValidItemName(string name)
        {
            XmlDocument doc = new XmlDocument();

            BuildItem item = new BuildItem(doc, name, "someItemSpec", new ItemDefinitionLibrary(new Project()));
            Assertion.AssertEquals(name, item.Name);
            Assertion.AssertEquals("someItemSpec", item.FinalItemSpec);
            // Try setter
            item.Name = name;
            Assertion.AssertEquals(name, item.Name);
        }

        [Test]
        public void BuildItemToTaskItemAndBack()
        {
            BuildItem[] buildItems = new BuildItem[2];

            buildItems[0] = new BuildItem("item1name", "item1$$value");
            buildItems[0].SetMetadata("Something", "Dir\\**");
            buildItems[0].SetMetadata("stupidescaping", "nokidding");

            buildItems[1] = new BuildItem("item2", "value2");
            buildItems[1].SetMetadata("name", "value");
            buildItems[1].SetMetadata("OtherName", ";Value;");

            ITaskItem[] pass1 = BuildItem.ConvertBuildItemArrayToTaskItems(buildItems);

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(pass1[i].ItemSpec, buildItems[i].FinalItemSpec);
                foreach (string metadataName in pass1[i].MetadataNames)
                {
                    Assert.AreEqual(pass1[i].GetMetadata(metadataName), buildItems[i].GetEvaluatedMetadata(metadataName));
                }
            }

            // This will create nameless BuildItems, so it's a different code path - that's why we need this and
            // the following test
            BuildItem[] pass2 = BuildItem.ConvertTaskItemArrayToBuildItems(pass1);

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(pass1[i].ItemSpec, pass2[i].FinalItemSpec);
                foreach (string metadataName in pass1[i].MetadataNames)
                {
                    Assert.AreEqual(pass1[i].GetMetadata(metadataName), pass2[i].GetEvaluatedMetadata(metadataName));
                }
            }

            ITaskItem[] pass3 = BuildItem.ConvertBuildItemArrayToTaskItems(pass2);

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(pass3[i].ItemSpec, pass2[i].FinalItemSpec);
                foreach (string metadataName in pass3[i].MetadataNames)
                {
                    Assert.AreEqual(pass3[i].GetMetadata(metadataName), pass2[i].GetEvaluatedMetadata(metadataName));
                }
            }

        }
    }
}
