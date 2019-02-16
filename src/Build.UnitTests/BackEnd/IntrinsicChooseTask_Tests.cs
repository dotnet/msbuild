// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Engine.UnitTests.Globbing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class IntrinsicChooseTask_Tests
    {

        [Fact]
        public void IntrinsicChooseWhenOtherwise()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <PropertyGroup>
                        <Example>A</Example>
                    </PropertyGroup>
                    <Target Name='t'>
                        <Choose>
                          <When Condition=""'$(Example)' == 'A'"">
                              <PropertyGroup>
                                  <ExampleIsA>true</ExampleIsA>
                              </PropertyGroup>
                          </When>
                          <Otherwise>
                              <PropertyGroup>
                                  <ExampleIsA>false</ExampleIsA>
                              </PropertyGroup>
                          </Otherwise>
                        </Choose>
                        <Message Text='[ExampleIsA=$(ExampleIsA)]'/>
                    </Target>
               </Project>
            "))));
            p.Build(new[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[ExampleIsA=true]");
        }

        [Fact]
        public void IntrinsicChooseWhenOtherwiseExecutesOtherwise()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <PropertyGroup>
                        <Example>B</Example>
                    </PropertyGroup>
                    <Target Name='t'>
                        <Choose>
                          <When Condition=""'$(Example)' == 'A'"">
                              <PropertyGroup>
                                  <ExampleIsA>true</ExampleIsA>
                              </PropertyGroup>
                          </When>
                          <Otherwise>
                              <PropertyGroup>
                                  <ExampleIsA>false</ExampleIsA>
                              </PropertyGroup>
                          </Otherwise>
                        </Choose>
                        <Message Text='[ExampleIsA=$(ExampleIsA)]'/>
                    </Target>
               </Project>
            "))));
            p.Build(new[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[ExampleIsA=false]");
        }

        /// <summary>
        /// Choose, When has true condition
        /// </summary>
        [Fact]
        public void IntrinsicChooseWhenTrue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                      <Target Name='t'>
                        <Choose>
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>      
                        </Choose>
                      </Target>
                    </Project>
                ");

            ProjectInstance instance = new Project(XmlReader.Create(new StringReader(content))).CreateProjectInstance();
            Assert.True(instance.Build("t", null));

            Assert.Equal("v1", instance.GetPropertyValue("p"));
            Assert.Equal("i1", Helpers.MakeList(instance.GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Choose, second When has true condition
        /// </summary>
        [Fact]
        public void IntrinsicChooseSecondWhenTrue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                      <Target Name='t'>
                        <Choose>
                            <When Condition='false'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>   
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>v2</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i2' />
                              </ItemGroup>
                            </When>    
                        </Choose>
                      </Target>
                    </Project>
                ");

            ProjectInstance instance = new Project(XmlReader.Create(new StringReader(content))).CreateProjectInstance();
            Assert.True(instance.Build("t", null));

            Assert.Equal("v2", instance.GetPropertyValue("p"));
            Assert.Equal("i2", Helpers.MakeList(instance.GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Choose, when used in targets with batching in when conditions
        /// </summary>
        [Fact]
        public void IntrinsicChooseBatching()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                          <i Include='i1' m1='mv1' />
                          <i Include='i2' m1='mv1' />
                          <i Include='i3' m1='mv2' />
                          <i Include='i4' m1='mv2' />
                          <i Include='i5' m1='mv3' />
                          <i Include='i6' m1='mv4' />
                        </ItemGroup>
                          <Target Name='t'>
                            <Choose>
                                <When Condition=""'%(i.m1)' == 'mv1'"">
                                  <ItemGroup>
                                    <mv1Items Include='@(i)' />
                                  </ItemGroup>
                                </When>
                                <When Condition=""'%(i.m1)' == 'mv2'"">
                                  <ItemGroup>
                                    <mv2Items Include='@(i)' />
                                  </ItemGroup>
                                </When>
                                <Otherwise>
                                  <ItemGroup>
                                    <otherItems Include='@(i)' />
                                  </ItemGroup>
                                </Otherwise>
                            </Choose>
                          </Target>
                    </Project>
                ");

            MockLogger logger = new MockLogger();
            ProjectInstance instance = new Project(XmlReader.Create(new StringReader(content))).CreateProjectInstance();
            Assert.True(instance.Build("t", new[] {logger}));

            var mv1Items = Helpers.MakeList(instance.GetItems("mv1Items"));
            var mv2Items = Helpers.MakeList(instance.GetItems("mv2Items"));
            var otherItems = Helpers.MakeList(instance.GetItems("otherItems"));

            mv1Items.Count.ShouldBe(2);
            mv1Items.ShouldContain(i => i.EvaluatedInclude == "i1");
            mv1Items.ShouldContain(i => i.EvaluatedInclude == "i2");

            mv2Items.Count.ShouldBe(2);
            mv2Items.ShouldContain(i => i.EvaluatedInclude == "i3");
            mv2Items.ShouldContain(i => i.EvaluatedInclude == "i4");

            otherItems.Count.ShouldBe(2);
            otherItems.ShouldContain(i => i.EvaluatedInclude == "i5");
            otherItems.ShouldContain(i => i.EvaluatedInclude == "i6");
        }
    }
}
