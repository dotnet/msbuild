// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Build.Evaluation;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    sealed public class CreateProperty_Tests
    {
        /// <summary>
        /// Load projects before every test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Clean up after every test (unload projects).
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to blank out a property value.
        /// </summary>
        [Test]
        public void CreateBlankProperty()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <NumberOfProcessors>Twenty</NumberOfProcessors>
                        </PropertyGroup>

                        <Target Name=`Build`>
                            <CreateProperty Value=``>
                                <Output PropertyName=`NumberOfProcessors` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`NumberOfProcessors='$(NumberOfProcessors)'`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("NumberOfProcessors=''");
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to create a property
        /// that has a parseable semicolon in it.
        /// </summary>
        [Test]
        public void CreatePropertyWithSemicolon()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <CreateProperty Value=`Clean ; Build`>
                                <Output PropertyName=`TargetsToRunLaterProperty` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`TargetsToRunLaterProperty = $(TargetsToRunLaterProperty)`/>
                            <CreateItem Include=`$(TargetsToRunLaterProperty)`>
                                <Output ItemName=`TargetsToRunLaterItem` TaskParameter=`Include`/>
                            </CreateItem>
                            <Message Text=`TargetsToRunLaterItem = @(TargetsToRunLaterItem,'----')`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("TargetsToRunLaterProperty = Clean;Build");
            logger.AssertLogContains("TargetsToRunLaterItem = Clean----Build");
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to create a property
        /// that has a literal semicolon in it.
        /// </summary>
        [Test]
        public void CreatePropertyWithLiteralSemicolon()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <CreateProperty Value=`Clean%3BBuild`>
                                <Output PropertyName=`TargetsToRunLaterProperty` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`TargetsToRunLaterProperty = $(TargetsToRunLaterProperty)`/>
                            <CreateItem Include=`$(TargetsToRunLaterProperty)`>
                                <Output ItemName=`TargetsToRunLaterItem` TaskParameter=`Include`/>
                            </CreateItem>
                            <Message Text=`TargetsToRunLaterItem = @(TargetsToRunLaterItem,'----')`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("TargetsToRunLaterProperty = Clean;Build");
            logger.AssertLogContains("TargetsToRunLaterItem = Clean;Build");
        }
    }
}
