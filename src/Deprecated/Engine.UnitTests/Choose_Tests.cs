// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using System.Threading;
using System.Collections;

namespace Microsoft.Build.UnitTests
{

    /*
     * Class:   ChooseTests
     * Owner:   davidle
     *
     * 
     */
    [TestFixture]
    sealed public class ChooseTests
    {
        /// <summary>
        /// Test stack overflow is prevented.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExcessivelyNestedChoose()
        {
            StringBuilder sb1 = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            for (int i = 0; i < 51; i++)
            {
                sb1.Append("<Choose><When Condition=`true`>");
                sb2.Append("</When></Choose>");
            }

            string project = "<Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>";
            project += sb1.ToString();
            project += "<PropertyGroup><foo>bar</foo></PropertyGroup>";
            project += sb2.ToString();
            project += @"
                    <Target Name=`t`>
                        <Message Text=`[$(foo)]`/>
                    </Target>
                </Project>";

            Project p = ObjectModelHelpers.CreateInMemoryProject(project);
            p.Build(new string[] { "t" }, null);

            // InvalidProjectFile exception expected
        }

        /*
         * Method:  Basic
         * Owner:   davidle
         *
         * 
         */
        [Test]
        public void ChooseNotTaken()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <Choose>
                    <When Condition=`1==2`>
                      <PropertyGroup><a>aa</a></PropertyGroup>
                    </When>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert(props["a"] == null);
        }

        /*
         * Method:  NeitherConditionTaken
         * Owner:   DavidLe
         *
         * Try a basic workings.
         */
        [Test]
        public void NeitherConditionTaken()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <Choose>
                    <When Condition=`1==2`>
                      <PropertyGroup><a>aa</a></PropertyGroup>
                    </When>
                    <When Condition=`1==3`>
                      <PropertyGroup><b>bb</b></PropertyGroup>
                    </When>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert(props["a"] == null);
            Assertion.Assert(props["b"] == null);
        }

        [Test]
        public void OtherwiseTaken()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <Choose>
                    <When Condition=`1==2`>
                      <PropertyGroup><a>aa</a></PropertyGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup><b>bb</b></PropertyGroup>
                    </Otherwise>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert(props["a"] == null);
            Assertion.Assert((string) props["b"] == "bb");
        }

        [Test]
        public void TwoOtherwiseErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        <When Condition=`true`><PropertyGroup><x/></PropertyGroup></When>
                        <Otherwise><PropertyGroup><y/></PropertyGroup></Otherwise>
                        <Otherwise><PropertyGroup><z/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void JunkAfterWhenErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        <When Condition=`true`>xyz
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise><PropertyGroup><y/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void JunkAfterOtherwiseErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        <When Condition=`true`>
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise>xyz
                          <PropertyGroup><y/></PropertyGroup>
                        </Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void BogusElementUnderChooseCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        <abc/>
                        <When Condition=`true`>
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise><PropertyGroup><y/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void ChooseWithConditionErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose Condition=`true`>
                        <When Condition=`true`>
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise><PropertyGroup><y/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }
        [Test]
        public void OtherwiseWithConditionErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        <When Condition=`true`>
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise Condition=`true`><PropertyGroup><y/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void JunkUnderChooseErrorCase()
        {
            bool fExceptionCaught;
            try
            {
                fExceptionCaught = false;
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <Choose>
                        abc
                        <When Condition=`true`>
                          <PropertyGroup><x/></PropertyGroup>
                        </When>
                        <Otherwise><PropertyGroup><y/></PropertyGroup></Otherwise>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void PropertyAssignmentToItemListCase()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`x1` />
                    <x Include=`x2` />
                  </ItemGroup>
                  <PropertyGroup>
                    <a>aa</a>
                    <b>@(x)</b>
                  </PropertyGroup>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert((string) props["a"] == "aa");
            Assertion.Assert((string) props["b"] == "@(x)");

            BuildItemGroup items = p.GetEvaluatedItemsByName("x");
            Assertion.AssertEquals(2, items.Count);
            Assertion.Assert(items[0].Include.CompareTo("x1") == 0);
            Assertion.Assert(items[1].Include.CompareTo("x2") == 0);
        }

        [Test]
        public void ItemListAndPropertiesCase()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <Choose>
                    <When Condition=`1==1`>
                        <ItemGroup><x Include=`x1`/></ItemGroup>
                    </When>
                  </Choose>
                  <Choose>
                    <When Condition=`1==1`>
                      <ItemGroup><x Include=`x2`/></ItemGroup>
                      <PropertyGroup><a>@(x)</a></PropertyGroup>
                    </When>
                    </Choose>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert((string) props["a"] == "@(x)");

            BuildItemGroup items = p.GetEvaluatedItemsByName("x");
            Assertion.AssertEquals(2, items.Count);
            Assertion.Assert(items[0].Include.CompareTo("x1") == 0);
            Assertion.Assert(items[1].Include.CompareTo("x2") == 0);
        }

        [Test]
        public void ItemGroupInAChooseConditionCase()
        {
            bool fExceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                      <ItemGroup><x Include=`x1`/></ItemGroup>
                      <Choose>
                        <When Condition=`@(x)==x1`>
                          <ItemGroup><y Include=`y1`/></ItemGroup>
                          <PropertyGroup><a>@(x)</a></PropertyGroup>
                        </When>
                      </Choose>
                      <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                      </Target>
                    </Project>
                ");
                p.Build(new string[] { "t" }, null);
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        [Test]
        public void NestedChooseAndPropertyInConditionCase()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <Choose>
                    <When Condition=`1==1`>
                      <PropertyGroup><a>true</a></PropertyGroup>
                      <Choose>
                        <When Condition=`$(a)`>
                          <PropertyGroup><b>bb</b></PropertyGroup>
                        </When>
                      </Choose>
                    </When>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`[$(a)]`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert((string) props["a"] == "true");
            Assertion.Assert((string) props["b"] == "bb");
        }

        [Test]
        public void ChooseTakesSameWhenInPass1And2()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <PropertyGroup><takefirst>true</takefirst></PropertyGroup>
                  <Choose>
                    <When Condition=`$(takefirst)`>
                      <PropertyGroup><takefirst>false</takefirst></PropertyGroup>
                      <ItemGroup><whichpass Include=`when1` /></ItemGroup>
                    </When>
                    <When Condition=`!$(takefirst)`>
                      <ItemGroup><whichpass Include=`when2` /></ItemGroup>
                    </When>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`foo`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildItemGroup items = p.GetEvaluatedItemsByName("whichpass");
            Assertion.AssertEquals(1, items.Count);
            Assertion.Assert(items[0].Include.CompareTo("when1") == 0);
        }

        [Test]
        public void ChooseTakesOtherwiseInPass1And2()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
            
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                  <PropertyGroup><takefirst>false</takefirst></PropertyGroup>
                  <PropertyGroup><tookfirst>false</tookfirst></PropertyGroup>
                  <Choose>
                    <When Condition=`$(takefirst)`>
                      <PropertyGroup><tookfirst>true</tookfirst></PropertyGroup>
                      <ItemGroup><whichpass Include=`when` /></ItemGroup>
                    </When>
                    <Otherwise>
                      <PropertyGroup><takefirst>true</takefirst></PropertyGroup>
                      <ItemGroup><whichpass Include=`otherwise` /></ItemGroup>
                    </Otherwise>
                  </Choose>
                  <Target Name=`t`>
                    <Message Text=`foo`/>
                  </Target>
                </Project>
            ");
            p.Build(new string[] { "t" }, null);
            BuildPropertyGroup props = p.EvaluatedProperties;
            Assertion.Assert((string) props["tookfirst"] == "false");
            BuildItemGroup items = p.GetEvaluatedItemsByName("whichpass");
            Assertion.AssertEquals(1, items.Count);
            Assertion.Assert(items[0].Include.CompareTo("otherwise") == 0);
        }
    }
}
