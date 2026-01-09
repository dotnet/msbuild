// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
#if FEATURE_CODETASKFACTORY

    using System.CodeDom.Compiler;
    using System.IO.Compression;
    using Microsoft.Build.Logging;
    using Microsoft.Build.Tasks.UnitTests;
    using Shouldly;

    public sealed class CodeTaskFactoryTests
    {
        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactory(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Code>
                                         Log.LogMessage(MessageImportance.High, Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num} Text=`Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
            }
        }

        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// Specifically testing that even when the ToolsVersion is post-4.0, and thus
        /// Microsoft.Build.Tasks.v4.0.dll is expected to NOT be in MSBuildToolsPath, that
        /// we will redirect under the covers to use the current tasks instead.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactory_RedirectFrom4(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Code>
                                         Log.LogMessage(MessageImportance.High, Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num} Text=`Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
                mockLogger.AssertLogDoesntContain("Microsoft.Build.Tasks.v4.0.dll");
            }
        }

        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// Specifically testing that even when the ToolsVersion is post-12.0, and thus
        /// Microsoft.Build.Tasks.v12.0.dll is expected to NOT be in MSBuildToolsPath, that
        /// we will redirect under the covers to use the current tasks instead.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactory_RedirectFrom12(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.v12.0.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Code>
                                         Log.LogMessage(MessageImportance.High, Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num} Text=`Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
                mockLogger.AssertLogDoesntContain("Microsoft.Build.Tasks.v12.0.dll");
            }
        }

        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// Specifically testing that even when the ToolsVersion is post-4.0, and we have redirection
        /// logic in place for the AssemblyFile case to deal with Microsoft.Build.Tasks.v4.0.dll not
        /// being in MSBuildToolsPath anymore, that this does NOT affect full fusion AssemblyNames --
        /// it's picked up from the GAC, where it is anyway, so there's no need to redirect.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactory_NoAssemblyNameRedirect(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num}` TaskFactory=`CodeTaskFactory` AssemblyName=`Microsoft.Build.Tasks.Core, Version=15.1.0.0` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Code>
                                         Log.LogMessage(MessageImportance.High, Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory{num} Text=`Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
                mockLogger.AssertLogContains("Microsoft.Build.Tasks.Core, Version=15.1.0.0");
            }
        }

        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// </summary>
        [Fact]
        public void VerifyRequiredAttribute()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_VerifyRequiredAttribute` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text Required='true'/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                     Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_VerifyRequiredAttribute/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            mockLogger.AssertLogContains("MSB4044");
        }

        /// <summary>
        /// Verify we get an error if a runtime exception is logged
        /// </summary>
        [Fact]
        public void RuntimeException()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_RuntimeException` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                     throw new InvalidOperationException(""MyCustomException"");
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_RuntimeException/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, true);
            mockLogger.AssertLogContains("MSB4018");
            mockLogger.AssertLogContains("MyCustomException");
        }

        /// <summary>
        /// Verify we get an error if a the languages attribute is set but it is empty
        /// </summary>
        [Fact]
        public void EmptyLanguage()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyLanguage` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Language=''>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyLanguage/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.AttributeEmpty");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Language"));
        }

        /// <summary>
        /// Verify we get an error if a the Type attribute is set but it is empty
        /// </summary>
        [Fact]
        public void EmptyType()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyType` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Type=''>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyType/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.AttributeEmpty");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Type"));
        }

        /// <summary>
        /// Verify we get an error if a the source attribute is set but it is empty
        /// </summary>
        [Fact]
        public void EmptySource()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptySource` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Source=''>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptySource/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.AttributeEmpty");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Source"));
        }

        /// <summary>
        /// Verify we get an error if a reference is missing an include attribute is set but it is empty
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("Include=\"\"")]
        [InlineData("Include=\" \"")]
        public void EmptyReferenceInclude(string includeSetting)
        {
            string taskName = "CustomTaskFromCodeFactory_EmptyReferenceInclude";
            string projectFileContents = @$"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`{taskName}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                 <Reference {includeSetting}/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <{taskName}/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.AttributeEmptyWithTaskElement");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Include", "Reference", taskName));
        }

        /// <summary>
        /// Verify we get an error if a Using statement is missing an namespace attribute is set but it is empty
        /// </summary>
        [Fact]
        public void EmptyUsingNamespace()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyUsingNamespace` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                 <Using/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyUsingNamespace/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.AttributeEmpty");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Namespace"));
        }

        /// <summary>
        /// Verify we get pass even if the reference is not a full path
        /// </summary>
        [Fact]
        public void ReferenceNotPath()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_ReferenceNotPath` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                 <Reference Include='System'/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_ReferenceNotPath Text=""Hello""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("Hello");
        }

        /// <summary>
        /// Verify we get an error a reference has strange chars
        /// </summary>
        [Fact]
        public void ReferenceInvalidChars()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_ReferenceInvalidChars` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                 <Reference Include='@@#$@#'/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_ReferenceInvalidChars/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            mockLogger.AssertLogContains("MSB3755");
            mockLogger.AssertLogContains("@@#$@#");
        }

        /// <summary>
        /// Verify we get an error if a using has invalid chars
        /// </summary>
        [Fact]
        public void UsingInvalidChars()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_UsingInvalidChars` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                 <Using Namespace='@@#$@#'/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_UsingInvalidChars/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            mockLogger.AssertLogContains("CS1646");
        }

        /// <summary>
        /// Verify we get an error if the sources points to an invalid file
        /// </summary>
        [Fact]
        public void SourcesInvalidFile()
        {
            string tempFileName = "Moose_" + Guid.NewGuid().ToString() + ".cs";

            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_SourcesInvalidFile` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Source='$(SystemDrive)\\" + tempFileName + @"'>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_SourcesInvalidFile/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            mockLogger.AssertLogContains(Environment.GetEnvironmentVariable("SystemDrive") + '\\' + tempFileName);
        }

        /// <summary>
        /// Verify we get an error if a the code element is missing
        /// </summary>
        [Fact]
        public void MissingCodeElement()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_MissingCodeElement` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_MissingCodeElement/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            mockLogger.AssertLogContains(String.Format(ResourceUtilities.GetResourceString("CodeTaskFactory.CodeElementIsMissing"), "CustomTaskFromCodeFactory_MissingCodeElement"));
        }

        /// <summary>
        /// Test the case where we have adding a using statement
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactoryTestExtraUsing(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestExtraUsing{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Using Namespace='System.Linq.Expressions'/>
                                    <Code>
                                          string linqString = ExpressionType.Add.ToString();
                                         Log.LogMessage(MessageImportance.High, linqString + Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestExtraUsing{num} Text=`:Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                string linqString = nameof(System.Linq.Expressions.ExpressionType.Add);
                mockLogger.AssertLogContains(linqString + ":Hello, World!");
            }
        }

        /// <summary>
        /// Verify setting the output tag on the parameter causes it to be an output from the perspective of the targets
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskDateCodeFactory(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`DateTaskFromCodeFactory_BuildTaskDateCodeFactory{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                                <ParameterGroup>
                                   <CurrentDate ParameterType=`System.String` Output=`true` />
                                </ParameterGroup>
                                <Task>
                                    <Code>
                                        CurrentDate = DateTime.Now.ToString();
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <DateTaskFromCodeFactory_BuildTaskDateCodeFactory{num}>
                                    <Output TaskParameter=`CurrentDate` PropertyName=`CurrentDate` />
                                </DateTaskFromCodeFactory_BuildTaskDateCodeFactory{num}>
                                <Message Text=`Current Date and Time: [[$(CurrentDate)]]` Importance=`High` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Current Date and Time:");
                mockLogger.AssertLogDoesntContain("[[]]");
            }
        }

        /// <summary>
        /// Verify that the vb language works and that creating the execute method also works
        /// </summary>
        [Fact]
        public void MethodImplmentationVB()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CodeMethod_MethodImplmentationVB` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Text ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Type='Method' Language='vb'>
 <![CDATA[
                             Public Overrides Function Execute() As Boolean
                                 Log.LogMessage(MessageImportance.High, Text)
                                 Return True
                             End Function
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <CodeMethod_MethodImplmentationVB Text='IAMVBTEXT'/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("IAMVBTEXT");
        }

        /// <summary>
        /// Verify that System does not need to be passed in as a extra reference when targeting vb
        /// </summary>
        [Fact]
        public void BuildTaskSimpleCodeFactoryTestSystemVB()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestSystemVB` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Language='vb'>
                                     Dim headerRequest As String
                                     headerRequest = System.Net.HttpRequestHeader.Accept.ToString()
                                     Log.LogMessage(MessageImportance.High, headerRequest + Text)
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestSystemVB Text=`:Hello, World!` />
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("Accept" + ":Hello, World!");
        }

        /// <summary>
        /// Verify that System does not need to be passed in as a extra reference when targeting c#
        /// </summary>
        [Fact]
        public void BuildTaskSimpleCodeFactoryTestSystemCS()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestSystemCS` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code Language='cs'>
                                     string headerRequest = System.Net.HttpRequestHeader.Accept.ToString();
                                     Log.LogMessage(MessageImportance.High, headerRequest + Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestSystemCS Text=`:Hello, World!` />
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("Accept" + ":Hello, World!");
        }

        /// <summary>
        /// Make sure we can pass in extra references than the automatic ones. For example the c# compiler does not pass in
        /// system.dll. So lets test that case
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuildTaskSimpleCodeFactoryTestExtraReference(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }
                string netFrameworkDirectory = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version45);
                if (netFrameworkDirectory == null)
                {
                    // "CouldNotFindRequiredTestDirectory"
                    return;
                }

                string systemNETLocation = Path.Combine(netFrameworkDirectory, "System.Net.dll");

                if (!File.Exists(systemNETLocation))
                {
                    // "CouldNotFindRequiredTestFile"
                    return;
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestExtraReferenceCS{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                    <Using Namespace='System.Net'/>
                                    <Reference Include='" + systemNETLocation + $@"'/>
                                    <Code>
                                         string netString = System.Net.HttpStatusCode.OK.ToString();
                                         Log.LogMessage(MessageImportance.High, netString + Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactoryTestExtraReferenceCS{num} Text=`:Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("OK" + ":Hello, World!");
            }
        }

        [Fact]
        public void OutOfProcCodeTaskFactoryCachesAssemblyPath()
        {
            try
                        {
                                const string taskElementContents = @"<Code Type=""Fragment"" Language=""cs"">
        Log.LogMessage(""inline execution"");
        return true;
    </Code>";

                var firstFactory = new Microsoft.Build.Tasks.CodeTaskFactory();
                var firstEngine = new MockEngine { ForceOutOfProcessExecution = true };
                bool initialized = firstFactory.Initialize(
                    "CachedCodeInlineTask",
                    new System.Collections.Generic.Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase),
                    taskElementContents,
                    firstEngine);
                initialized.ShouldBeTrue(firstEngine.Log);

                ITask firstTask = firstFactory.CreateTask(firstEngine);
                firstTask.ShouldNotBeNull();

                string firstAssemblyPath = ((IOutOfProcTaskFactory)firstFactory).GetAssemblyPath();
                firstAssemblyPath.ShouldNotBeNullOrEmpty();
                File.Exists(firstAssemblyPath).ShouldBeTrue();

                firstFactory.CleanupTask(firstTask);

                var secondFactory = new Microsoft.Build.Tasks.CodeTaskFactory();
                var secondEngine = new MockEngine { ForceOutOfProcessExecution = true };
                bool initializedAgain = secondFactory.Initialize(
                    "CachedCodeInlineTask",
                    new System.Collections.Generic.Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase),
                    taskElementContents,
                    secondEngine);
                initializedAgain.ShouldBeTrue(secondEngine.Log);

                ITask secondTask = secondFactory.CreateTask(secondEngine);
                secondTask.ShouldNotBeNull();

                string reusedAssemblyPath = ((IOutOfProcTaskFactory)secondFactory).GetAssemblyPath();
                reusedAssemblyPath.ShouldBe(firstAssemblyPath);
                File.Exists(reusedAssemblyPath).ShouldBeTrue();

                secondFactory.CleanupTask(secondTask);
            }
            finally
            {
            }
        }

        /// <summary>
        /// jscript .net works
        /// </summary>
        [Fact]
        public void MethodImplementationJScriptNet()
        {
            if (!CodeDomProvider.IsDefinedLanguage("js"))
            {
                // "JScript .net Is not installed on the test machine this test cannot run"
                return;
            }

            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CodeMethod_MethodImplementationJScriptNet` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Text ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Type='Method' Language='js'>
 <![CDATA[
                             override function Execute() : System.Boolean
                             {
                                 Log.LogMessage(MessageImportance.High, Text);
                                 return true;
                             }
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <CodeMethod_MethodImplementationJScriptNet Text='IAMJSTEXT'/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("IAMJSTEXT");
        }

        /// <summary>
        /// Verify we can set a code type of Method which expects us to override the execute method entirely.
        /// </summary>
        [Fact]
        public void MethodImplementation()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CodeMethod_MethodImplementation` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Text ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Type='Method'>
 <![CDATA[
                                public override bool Execute()
                                {
                                    Log.LogMessage(MessageImportance.High, Text);
                                    return true;
                                }
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <CodeMethod_MethodImplementation Text='IAMTEXT'/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("IAMTEXT");
        }

        /// <summary>
        /// Verify we can set the type to Class and this expects an entire class to be entered into the code tag
        /// </summary>
        [Fact]
        public void ClassImplementationTest()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`LogNameValue_ClassImplementationTest` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Name ParameterType='System.String' />
                            <Value ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Type='Class'>
 <![CDATA[
                    using System;
                    using System.Collections.Generic;
                    using System.Text;
                    using Microsoft.Build.Utilities;
                    using Microsoft.Build.Framework;

                    namespace Microsoft.Build.NonShippingTasks
                    {
                        public class LogNameValue_ClassImplementationTest : Task
                        {
                            private string variableName;
                            private string variableValue;


                            [Required]
                            public string Name
                            {
                                get { return variableName; }
                                set { variableName = value; }
                            }


                            public string Value
                            {
                                get { return variableValue; }
                                set { variableValue = value; }
                            }


                            public override bool Execute()
                            {
                                // Set the process environment
                                Log.LogMessage(""Setting {0}={1}"", this.variableName, this.variableValue);
                                return true;
                            }
                        }
                    }
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <LogNameValue_ClassImplementationTest Name='MyName' Value='MyValue'/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
            mockLogger.AssertLogContains("MyName=MyValue");
        }

        /// <summary>
        /// Verify we can set the type to Class and this expects an entire class to be entered into the code tag
        /// </summary>
        [Fact]
        public void ClassImplementationTestDoesNotInheritFromITask()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`ClassImplementationTestDoesNotInheritFromITask` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Name ParameterType='System.String' />
                            <Value ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Type='Class'>
 <![CDATA[
                    using System;
                    using System.Collections.Generic;
                    using System.Text;
                    using Microsoft.Build.Utilities;
                    using Microsoft.Build.Framework;

                    namespace Microsoft.Build.NonShippingTasks
                    {
                        public class ClassImplementationTestDoesNotInheritFromITask
                        {
                            private string variableName;

                            [Required]
                            public string Name
                            {
                                get { return variableName; }
                                set { variableName = value; }
                            }

                            public bool Execute()
                            {
                                // Set the process environment
                                Console.Out.WriteLine(variableName);
                                return true;
                            }
                        }
                    }
  ]]>
                            </Code>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <ClassImplementationTestDoesNotInheritFromITask Name='MyName' Value='MyValue'/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.NeedsITaskInterface");
            mockLogger.AssertLogContains(unformattedMessage);
        }

        /// <summary>
        /// Verify we get an error if a the Type attribute is set but it is empty
        /// </summary>
        [Fact]
        public void MultipleCodeElements()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyType` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyType/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.MultipleCodeNodes");
            mockLogger.AssertLogContains(unformattedMessage);
        }

        /// <summary>
        /// Verify we get an error if a the Type attribute is set but it is empty
        /// </summary>
        [Fact]
        public void ReferenceNestedInCode()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyType` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                      <Reference Include=""System.Xml""/>
                                       <Using Namespace=""Hello""/>
                                        <Task/>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyType/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.InvalidElementLocation");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Reference", "Code"));
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Using", "Code"));
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Task", "Code"));
        }

        /// <summary>
        /// Verify we get an error if there is an unknown element in the task tag
        /// </summary>
        [Fact]
        public void UnknownElementInTask()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_EmptyType` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                              <Unknown/>
                                <Code>
                                       Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_EmptyType Text=""HELLO""/>
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, false);
            string unformattedMessage = ResourceUtilities.GetResourceString("CodeTaskFactory.InvalidElementLocation");
            mockLogger.AssertLogContains(String.Format(unformattedMessage, "Unknown", "Task"));
        }

        /// <summary>
        /// Verify we can set a source file location and this will be read in and used.
        /// </summary>
        [Fact]
        public void ClassSourcesTest()
        {
            string sourceFileContent = @"
                                       using System;
                    using System.Collections.Generic;
                    using System.Text;
                    using Microsoft.Build.Utilities;
                    using Microsoft.Build.Framework;

                    namespace Microsoft.Build.NonShippingTasks
                    {
                        public class LogNameValue_ClassSourcesTest : Task
                        {
                            private string variableName;
                            private string variableValue;


                            [Required]
                            public string Name
                            {
                                get { return variableName; }
                                set { variableName = value; }
                            }


                            public string Value
                            {
                                get { return variableValue; }
                                set { variableValue = value; }
                            }


                            public override bool Execute()
                            {
                                // Set the process environment
                                Log.LogMessage(""Setting {0}={1}"", this.variableName, this.variableValue);
                                return true;
                            }
                        }
                    }
";

            string tempFileDirectory = Path.GetTempPath();
            string tempFileName = Guid.NewGuid().ToString() + ".cs";
            string tempSourceFile = Path.Combine(tempFileDirectory, tempFileName);
            File.WriteAllText(tempSourceFile, sourceFileContent);

            try
            {
                string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`LogNameValue_ClassSourcesTest` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll`>
                        <ParameterGroup>
                            <Name ParameterType='System.String' />
                            <Value ParameterType='System.String' />
                        </ParameterGroup>
                        <Task>
                            <Code Source='" + tempSourceFile + @"'/>
                         </Task>
                         </UsingTask>
                        <Target Name=`Build`>
                            <LogNameValue_ClassSourcesTest Name='MyName' Value='MyValue'/>
                        </Target>
                    </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("MyName=MyValue");
            }
            finally
            {
                if (File.Exists(tempSourceFile))
                {
                    File.Delete(tempSourceFile);
                }
            }
        }

        /// <summary>
        /// Code factory test where the TMP directory does not exist.
        /// See https://github.com/dotnet/msbuild/issues/328 for details.
        /// </summary>
        [Fact]
        public void BuildTaskSimpleCodeFactoryTempDirectoryDoesntExist()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                     Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory Text=`Hello, World!` />
                        </Target>
                    </Project>";

            var oldTempPath = Environment.GetEnvironmentVariable("TMP");
            var newTempPath = Path.Combine(Path.GetFullPath(oldTempPath), Path.GetRandomFileName());

            try
            {
                // Ensure we're getting the right temp path (%TMP% == GetTempPath())
                Assert.Equal(
                    FileUtilities.EnsureTrailingSlash(Path.GetTempPath()),
                    FileUtilities.EnsureTrailingSlash(Path.GetFullPath(oldTempPath)));
                Assert.False(Directory.Exists(newTempPath));

                Environment.SetEnvironmentVariable("TMP", newTempPath);

                Assert.Equal(
                    FileUtilities.EnsureTrailingSlash(newTempPath),
                    FileUtilities.EnsureTrailingSlash(Path.GetTempPath()));

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTempPath);
                FileUtilities.DeleteDirectoryNoThrow(newTempPath, true);
            }
        }

        /// <summary>
        /// Test the simple case where we have a string parameter and we want to log that.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RedundantMSBuildReferences(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string projectFileContents = $@"
                        <Project ToolsVersion='msbuilddefaulttoolsversion'>
                            <UsingTask TaskName=`CustomTaskFromCodeFactory_RedundantMSBuildReferences{num}` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                             <ParameterGroup>
                                 <Text/>
                              </ParameterGroup>
                                <Task>
                                  <Reference Include='$(MSBuildToolsPath)\Microsoft.Build.Framework.dll' />
                                  <Reference Include='$(MSBuildToolsPath)\Microsoft.Build.Utilities.Core.dll' />

                                    <Code>
                                         Log.LogMessage(MessageImportance.High, Text);
                                    </Code>
                                </Task>
                            </UsingTask>
                            <Target Name=`Build`>
                                <CustomTaskFromCodeFactory_RedundantMSBuildReferences{num} Text=`Hello, World!` />
                            </Target>
                        </Project>";

                MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectSuccess(projectFileContents);
                mockLogger.AssertLogContains("Hello, World!");
            }
        }

        /// <summary>
        /// Verify that the generated code from source is embedded in the binlog
        /// </summary>
        /// <param name="forceOutOfProc"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmbedsGeneratedFromSourceFileInBinlog(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 1 : 0;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string taskName = $"HelloTask{num}";

                string sourceContent = $$"""
                    namespace InlineTask
                    {
                        using Microsoft.Build.Utilities;

                        public class {{taskName}} : Task
                        {
                            public override bool Execute()
                            {
                                Log.LogMessage("Hello, world!");
                                return !Log.HasLoggedErrors;
                            }
                        }
                    }
                    """;

                CodeTaskFactoryEmbeddedFileInBinlogTestHelper.BuildFromSourceAndCheckForEmbeddedFileInBinlog(
                    FactoryType.CodeTaskFactory, taskName, sourceContent, true);
            }
        }

        /// <summary>
        /// Verify that the generated code from source is embedded in the binlog even when it fails to compile
        /// </summary>
        /// <param name="forceOutOfProc"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmbedsGeneratedFromSourceFileInBinlogWhenFailsToCompile(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 3 : 2;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string taskName = $"HelloTask{num}";

                string sourceContent = $$"""
                    namespace InlineTask
                    {
                        using Microsoft.Build.Utilities;

                        public class {{taskName}} : Task
                        {
                """;

                CodeTaskFactoryEmbeddedFileInBinlogTestHelper.BuildFromSourceAndCheckForEmbeddedFileInBinlog(
                    FactoryType.CodeTaskFactory, taskName, sourceContent, false);
            }
        }

        /// <summary>
        /// Verify that the generated code is embedded in the binlog
        /// </summary>
        /// <param name="forceOutOfProc"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmbedsGeneratedFileInBinlog(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 5 : 4;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string taskXml = @"
                <Task>
                    <Code Type=""Fragment"" Language=""cs"">
                        <![CDATA[
                              Log.LogMessage(""Hello, World!"");
                		   ]]>
                    </Code>
                </Task>";

                CodeTaskFactoryEmbeddedFileInBinlogTestHelper.BuildAndCheckForEmbeddedFileInBinlog(
                    FactoryType.CodeTaskFactory, $"HelloTask{num}", taskXml, true);
            }
        }

        /// <summary>
        /// Verify that the generated code is embedded in the binlog even when it fails to compile
        /// </summary>
        /// <param name="forceOutOfProc"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmbedsGeneratedFileInBinlogWhenFailsToCompile(bool forceOutOfProc)
        {
            int num = forceOutOfProc ? 7 : 6;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                if (forceOutOfProc)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEINLINETASKFACTORIESOUTOFPROC", "1");
                }

                string taskXml = @"
                <Task>
                    <Code Type=""Fragment"" Language=""cs"">
                        <![CDATA[
                              Log.LogMessage(""Hello, World!
                		   ]]>
                    </Code>
                </Task>";

                CodeTaskFactoryEmbeddedFileInBinlogTestHelper.BuildAndCheckForEmbeddedFileInBinlog(
                    FactoryType.CodeTaskFactory, $"HelloTask{num}", taskXml, false);
            }
        }

        [Fact]
        public void ShouldEmitSingleGeneratedFileIntoBinlog()
        {
            using var env = TestEnvironment.Create();

            // Define task XML for Import.targets
            string taskXml = @"
                <Project>
                  <UsingTask
                    TaskName=""CustomTask""
                    TaskFactory=""CodeTaskFactory""
                    AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
                    <ParameterGroup>
                      <InputParameter ParameterType=""System.String"" />
                      <OutputParameter ParameterType=""System.String"" Output=""True"" />
                    </ParameterGroup>
                    <Task>
                      <Using Namespace=""System"" />
                      <Code Type=""Fragment"" Language=""cs"">
                        <![CDATA[
                            Console.WriteLine(this.InputParameter);
                            this.OutputParameter = ""Hello "" + this.InputParameter;
                        ]]>
                      </Code>
                    </Task>
                  </UsingTask>
                </Project>";

            TransientTestFile importTargetsFile = env.CreateFile("Import.targets", taskXml);

            // Define Another.proj content
            string anotherContent = $@"<Project>
                <Import Project=""{importTargetsFile.Path}"" />
                <Target Name=""AnotherTarget"">
                    <CustomTask InputParameter=""Foo"">
                        <Output PropertyName=""TaskOutput"" TaskParameter=""OutputParameter"" />
                    </CustomTask>
                    <Message Text=""Output: $(TaskOutput)"" />
                </Target>
            </Project>";

            TransientTestFile anotherProjFile = env.CreateFile("Another.proj", anotherContent);

            // Define main.csproj content
            string projectFileContent = $@"<Project>
                <Import Project=""{importTargetsFile.Path}"" />
                <Target Name=""Build"">
                    <MSBuild Projects=""{anotherProjFile.Path}"" Targets=""AnotherTarget"" />
                    <CustomTask InputParameter=""Bar"" />
                </Target>
            </Project>";

            TransientTestFile binlog = env.ExpectFile(".binlog");

            var binaryLogger = new BinaryLogger()
            {
                Parameters = $"LogFile={binlog.Path}",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            };

            Helpers.BuildProjectWithNewOMAndBinaryLogger(projectFileContent, binaryLogger, out bool result, out string projectDirectory);

            Assert.True(result);

            string projectImportsZipPath = Path.ChangeExtension(binlog.Path, ".ProjectImports.zip");
            using var fileStream = new FileStream(projectImportsZipPath, FileMode.Open);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // check to make sure that only 1 tmp file is created
            var tmpFiles = zipArchive.Entries.Where(zE => zE.Name.EndsWith("CustomTask-compilation-file.tmp")).ToList();
            tmpFiles.Count.ShouldBe(1, $"Expected exactly one file ending with 'CustomTask-compilation-file.tmp' in ProjectImports.zip, but found {tmpFiles.Count}.");
        }
                /// <summary>
        /// Verifies that ITaskFactoryBuildParameterProvider.IsMultiThreadedBuild triggers out-of-process compilation
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MultiThreadedBuildTriggersOutOfProcCompilation(bool isMultiThreaded)
        {
            MockEngine buildEngine = new MockEngine { IsMultiThreadedBuild = isMultiThreaded };

            Microsoft.Build.Tasks.CodeTaskFactory factory = new Microsoft.Build.Tasks.CodeTaskFactory();

            string taskBody = @"
<Code Type=""Fragment"" Language=""cs"">
    <![CDATA[
    Log.LogMessage(""Hello from CodeTaskFactory"");
    ]]>
</Code>";

            string taskName = isMultiThreaded ? "TestTaskMultiThreaded" : "TestTaskSingleThreaded";
            bool success = factory.Initialize(taskName, new System.Collections.Generic.Dictionary<string, TaskPropertyInfo>(), taskBody, buildEngine);
            success.ShouldBeTrue();

            // Get assembly path - should be non-null when compiled for out-of-proc
            string assemblyPath = ((IOutOfProcTaskFactory)factory).GetAssemblyPath();

            if (isMultiThreaded)
            {
                assemblyPath.ShouldNotBeNullOrEmpty();
                File.Exists(assemblyPath).ShouldBeTrue();
            }
            else
            {
                // In-memory compilation may not produce a file
            }
        }

        /// <summary>
        /// Verifies that ForceOutOfProcessExecution property triggers out-of-proc compilation
        /// </summary>
        [Fact]
        public void ForceOutOfProcessExecutionTriggersOutOfProcCompilation()
        {
            MockEngine buildEngine = new MockEngine 
            { 
                ForceOutOfProcessExecution = true,
                IsMultiThreadedBuild = false 
            };

            Microsoft.Build.Tasks.CodeTaskFactory factory = new Microsoft.Build.Tasks.CodeTaskFactory();

            string taskBody = @"
<Code Type=""Fragment"" Language=""cs"">
    <![CDATA[
    Log.LogMessage(""Hello"");
    ]]>
</Code>";

            bool success = factory.Initialize("TestTaskForced", new System.Collections.Generic.Dictionary<string, TaskPropertyInfo>(), taskBody, buildEngine);
            success.ShouldBeTrue();

            string assemblyPath = ((IOutOfProcTaskFactory)factory).GetAssemblyPath();
            assemblyPath.ShouldNotBeNullOrEmpty();
            File.Exists(assemblyPath).ShouldBeTrue();
        }

        /// <summary>
        /// End-to-end test that verifies inline tasks execute successfully when /mt is used.
        /// This confirms the inline task factory compiles for out-of-process execution and the task runs correctly.
        /// </summary>
        [Fact]
        public void MultiThreadedBuildExecutesInlineTasksSuccessfully()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFolder folder = env.CreateFolder(createFolder: true);
                
                // Create a project with an inline task using CodeTaskFactory
                TransientTestFile projectFile = env.CreateFile(folder, "test.proj", @"
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  
  <!-- Define an inline task using CodeTaskFactory -->
  <UsingTask TaskName=""MyCodeTask"" TaskFactory=""CodeTaskFactory"" AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
    <ParameterGroup>
      <Message ParameterType=""System.String"" Required=""true"" />
      <OutputValue ParameterType=""System.String"" Output=""true"" />
    </ParameterGroup>
    <Task>
      <Code Type=""Fragment"" Language=""cs"">
        <![CDATA[
        Log.LogMessage(MessageImportance.High, ""Code task executed: "" + Message);
        OutputValue = ""Success from code task"";
        return true;
        ]]>
      </Code>
    </Task>
  </UsingTask>

  <Target Name=""Build"">
    <Message Text=""Starting code task test..."" Importance=""High"" />
    <MyCodeTask Message=""Hello from multi-threaded build!"">
      <Output TaskParameter=""OutputValue"" PropertyName=""TaskResult"" />
    </MyCodeTask>
    <Message Text=""Task result: $(TaskResult)"" Importance=""High"" />
    <Error Text=""Code task did not produce expected output"" Condition=""'$(TaskResult)' != 'Success from code task'"" />
  </Target>

</Project>");

                // Build with /mt flag with detailed verbosity to see task launching details
                string output = Microsoft.Build.UnitTests.Shared.RunnerUtilities.ExecMSBuild(
                    projectFile.Path + " /t:Build /mt /v:detailed", 
                    out bool success);

                success.ShouldBeTrue();
                output.ShouldContain("Code task executed: Hello from multi-threaded build!");
                output.ShouldContain("Task result: Success from code task");
                
                // Verify the inline task was launched from a temporary assembly (out-of-process execution)
                output.ShouldContain(".inline_task.dll");
                output.ShouldContain("external task host");
            }
        }
    }
#else
    public sealed class CodeTaskFactoryTests
    {
        [Fact]
        public void CodeTaskFactoryNotSupported()
        {
            string projectFileContents = @"
                    <Project ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName=`CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory` TaskFactory=`CodeTaskFactory` AssemblyFile=`$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll` >
                         <ParameterGroup>
                             <Text/>
                          </ParameterGroup>
                            <Task>
                                <Code>
                                     Log.LogMessage(MessageImportance.High, Text);
                                </Code>
                            </Task>
                        </UsingTask>
                        <Target Name=`Build`>
                            <CustomTaskFromCodeFactory_BuildTaskSimpleCodeFactory Text=`Hello, World!` />
                        </Target>
                    </Project>";

            MockLogger mockLogger = Helpers.BuildProjectWithNewOMExpectFailure(projectFileContents, allowTaskCrash: false);

            BuildErrorEventArgs error = mockLogger.Errors.FirstOrDefault();

            Assert.NotNull(error);
            Assert.Equal("MSB4801", error.Code);
            Assert.Contains("CodeTaskFactory", error.Message);
        }
    }
#endif
}
