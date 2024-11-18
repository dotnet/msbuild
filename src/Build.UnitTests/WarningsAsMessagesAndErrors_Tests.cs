// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests
{
    public sealed class WarningsAsMessagesAndErrorsTests
    {
        private const string ExpectedEventMessage = "03767942CDB147B98D0ECDBDE1436DA3";
        private const string ExpectedEventCode = "0BF68998";
        private ITestOutputHelper _output;

        public WarningsAsMessagesAndErrorsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TreatAllWarningsAsErrors()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(GetTestProject(treatAllWarningsAsErrors: true));

            VerifyBuildErrorEvent(logger);

            ObjectModelHelpers.BuildProjectExpectSuccess(GetTestProject(treatAllWarningsAsErrors: false));
        }

        [Fact]
        public void TreatAllWarningsAsErrorsNoPrefix()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(GetTestProject(customProperties: new Dictionary<string, string>
            {
                {"TreatWarningsAsErrors", "true"},
            }));

            VerifyBuildErrorEvent(logger);

            ObjectModelHelpers.BuildProjectExpectSuccess(GetTestProject(treatAllWarningsAsErrors: false));
        }

        /// <summary>
        /// https://github.com/dotnet/msbuild/issues/2667
        /// </summary>
        [Fact]
        public void TreatWarningsAsErrorsWhenBuildingSameProjectMultipleTimes()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles project2 = testEnvironment.CreateTestProjectWithFiles($@"
                <Project xmlns=""msbuildnamespace"">
                    <PropertyGroup>
                        <MSBuildWarningsAsErrors>{ExpectedEventCode}</MSBuildWarningsAsErrors>
                    </PropertyGroup>
                    <Target Name=""Build"">
                        <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Targets=""AnotherTarget"" />
                    </Target>
                    <Target Name=""AnotherTarget"">
                        <Warning Text=""{ExpectedEventMessage}"" Code=""{ExpectedEventCode}"" />
                    </Target>
                </Project>");

                TransientTestProjectWithFiles project1 = testEnvironment.CreateTestProjectWithFiles($@"
                <Project xmlns=""msbuildnamespace"">
                    <Target Name=""Build"">
                        <MSBuild Projects=""{project2.ProjectFile}"" Targets=""Build"" />
                    </Target>
                </Project>");

                MockLogger logger = project1.BuildProjectExpectFailure(validateLoggerRoundtrip: false);

                VerifyBuildErrorEvent(logger);
            }
        }

        [Fact]
        public void TreatWarningsAsErrorsWhenSpecified()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(GetTestProject(warningsAsErrors: ExpectedEventCode));

            VerifyBuildErrorEvent(logger);
        }

        [Fact]
        public void TreatWarningsAsErrorsWhenSpecifiedIndirectly()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(
                GetTestProject(
                    customProperties: new Dictionary<string, string>
                    {
                        {"Foo", "true"},
                        {"MSBuildTreatWarningsAsErrors", "$(Foo)"}
                    }));

            VerifyBuildErrorEvent(logger);
        }

        [Fact]
        public void NotTreatWarningsAsErrorsWhenCodeNotSpecified()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", "123"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", "$(MSBuildWarningsAsErrors);ABC")
                    }));

            VerifyBuildWarningEvent(logger);
        }

        [Fact]
        public void TreatWarningsAsMessagesWhenSpecified()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(GetTestProject(warningsAsMessages: ExpectedEventCode));

            VerifyBuildMessageEvent(logger);
        }

        /// <summary>
        /// https://github.com/dotnet/msbuild/issues/2667
        /// </summary>
        [Fact]
        public void TreatWarningsAsMessagesWhenBuildingSameProjectMultipleTimes()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles project2 = testEnvironment.CreateTestProjectWithFiles($@"
                <Project xmlns=""msbuildnamespace"">
                    <PropertyGroup>
                        <MSBuildWarningsAsMessages>{ExpectedEventCode}</MSBuildWarningsAsMessages>
                    </PropertyGroup>
                    <Target Name=""Build"">
                        <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Targets=""AnotherTarget"" />
                    </Target>
                    <Target Name=""AnotherTarget"">
                        <Warning Text=""{ExpectedEventMessage}"" Code=""{ExpectedEventCode}"" />
                    </Target>
                </Project>");

                TransientTestProjectWithFiles project1 = testEnvironment.CreateTestProjectWithFiles($@"
                <Project xmlns=""msbuildnamespace"">
                    <Target Name=""Build"">
                        <MSBuild Projects=""{project2.ProjectFile}"" Targets=""Build"" />
                    </Target>
                </Project>");

                MockLogger logger = project1.BuildProjectExpectSuccess(validateLoggerRoundtrip: false);

                VerifyBuildMessageEvent(logger);
            }
        }

        [Fact]
        public void TreatWarningsAsMessagesWhenSpecifiedIndirectly()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new Dictionary<string, string>
                    {
                        {"Foo", ExpectedEventCode},
                    },
                    warningsAsMessages: "$(Foo)"));

            VerifyBuildMessageEvent(logger);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TreatWarningsAsMessagesWhenSpecifiedThroughAdditiveProperty(bool usePrefix)
        {
            string prefix = usePrefix ? "MSBuild" : "";
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>($"{prefix}WarningsAsMessages", "123"),
                        new KeyValuePair<string, string>($"{prefix}WarningsAsMessages", $@"$({prefix}WarningsAsMessages);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>($"{prefix}WarningsAsMessages", $"$({prefix}WarningsAsMessages);ABC")
                    }));

            VerifyBuildMessageEvent(logger);
        }

        [Fact]
        ///
        /// This is for chaining the properties together via addition.
        /// Furthermore it is intended to check if the prefix and no prefix variant interacts properly with each other.
        ///
        public void TreatWarningsAsMessagesWhenSpecifiedThroughAdditivePropertyCombination()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "123"),
                        new KeyValuePair<string, string>("WarningsAsMessages", $@"$(MSBuildWarningsAsMessages);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "$(WarningsAsMessages);ABC")
                    }));

            VerifyBuildMessageEvent(logger);
        }

        [Fact]
        public void TreatWarningsNotAsErrorsWhenSpecifiedThroughAdditivePropertyCombination()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsNotAsErrors", "123"),
                        new KeyValuePair<string, string>("WarningsNotAsErrors", $@"$(MSBuildWarningsNotAsErrors);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>("MSBuildWarningsNotAsErrors", "$(WarningsNotAsErrors);ABC")
                    }),
                _output);

            VerifyBuildWarningEvent(logger);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TreatWarningsAsErrorsWhenSpecifiedThroughAdditiveProperty(bool MSBuildPrefix)
        {
            string prefix = MSBuildPrefix ? "MSBuild" : "";
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>($@"{prefix}WarningsAsErrors", "123"),
                        new KeyValuePair<string, string>($@"{prefix}WarningsAsErrors", $@"$({prefix}WarningsAsErrors);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>($@"{prefix}WarningsAsErrors", $@"$({prefix}WarningsAsErrors);ABC")
                    }),
                _output);

            VerifyBuildErrorEvent(logger);
        }

        [Fact]
        public void TreatWarningsAsErrorsWhenSpecifiedThroughAdditivePropertyCombination()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("WarningsAsErrors", "123"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", $@"$(WarningsAsErrors);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>("WarningsAsErrors", "$(MSBuildWarningsAsErrors);ABC")
                    }),
                _output);

            VerifyBuildErrorEvent(logger);
        }

        [Fact]
        public void NotTreatWarningsAsMessagesWhenCodeNotSpecified()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "123"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "$(MSBuildWarningsAsMessages);ABC")
                    }),
                _output);

            VerifyBuildWarningEvent(logger);
        }

        [Fact]
        public void TreatWarningAsMessageOverridesTreatingItAsError()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    warningsAsMessages: ExpectedEventCode,
                    warningsAsErrors: ExpectedEventCode));

            VerifyBuildMessageEvent(logger);
        }

        private void VerifyBuildErrorEvent(MockLogger logger)
        {
            BuildErrorEventArgs actualEvent = logger.Errors.FirstOrDefault();

            Assert.NotNull(actualEvent);

            Assert.Equal(ExpectedEventMessage, actualEvent.Message);
            Assert.Equal(ExpectedEventCode, actualEvent.Code);

            logger.AssertNoWarnings();
        }

        private void VerifyBuildWarningEvent(MockLogger logger)
        {
            BuildWarningEventArgs actualEvent = logger.Warnings.FirstOrDefault();

            Assert.NotNull(actualEvent);

            Assert.Equal(ExpectedEventMessage, actualEvent.Message);
            Assert.Equal(ExpectedEventCode, actualEvent.Code);

            logger.AssertNoErrors();
        }

        private void VerifyBuildMessageEvent(MockLogger logger)
        {
            BuildMessageEventArgs actualEvent = logger.BuildMessageEvents.FirstOrDefault(i => i.Message.Equals(ExpectedEventMessage));

            Assert.NotNull(actualEvent);

            Assert.Equal(ExpectedEventCode, actualEvent.Code);

            logger.AssertNoErrors();
            logger.AssertNoWarnings();
        }

        private string GetTestProject(bool? treatAllWarningsAsErrors = null, string warningsAsErrors = null, string warningsAsMessages = null, ICollection<KeyValuePair<string, string>> customProperties = null)
        {
            return $@"
            <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                <PropertyGroup>
                    {(customProperties != null ? String.Join(Environment.NewLine, customProperties.Select(i => $"<{i.Key}>{i.Value}</{i.Key}>")) : "")}
                    {(treatAllWarningsAsErrors.HasValue ? $"<MSBuildTreatWarningsAsErrors>{treatAllWarningsAsErrors.Value}</MSBuildTreatWarningsAsErrors>" : "")}
                    {(warningsAsErrors != null ? $"<MSBuildWarningsAsErrors>{warningsAsErrors}</MSBuildWarningsAsErrors>" : "")}
                    {(warningsAsMessages != null ? $"<MSBuildWarningsAsMessages>{warningsAsMessages}</MSBuildWarningsAsMessages>" : "")}
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text=""MSBuildTreatWarningsAsErrors: '$(MSBuildTreatWarningsAsErrors)' "" />
                    <Message Text=""MSBuildWarningsAsErrors: '$(MSBuildWarningsAsErrors)' "" />
                    <Message Text=""MSBuildWarningsAsMessages: '$(MSBuildWarningsAsMessages)' "" />
                    <Warning Text=""{ExpectedEventMessage}"" Code=""{ExpectedEventCode}"" />
                </Target>
            </Project>";
        }


        [Theory]

        [InlineData("MSB1235", "MSB1234", "MSB1234", "MSB1234", false)] // Log MSB1234, treat as error via MSBuildWarningsAsErrors
        [InlineData("MSB1235", "", "MSB1234", "MSB1234", true)] // Log MSB1234, expect MSB1234 as error via MSBuildTreatWarningsAsErrors
        [InlineData("MSB1234", "MSB1234", "MSB1234", "MSB4181", true)]// Log MSB1234, MSBuildWarningsAsMessages takes priority
        [InlineData("MSB1235", "MSB1234", "MSB1234", "MSB1234", false, false)] // Log MSB1234, treat as error via BuildWarningsAsErrors
        [InlineData("MSB1235", "", "MSB1234", "MSB1234", true, false)] // Log MSB1234, expect MSB1234 as error via BuildTreatWarningsAsErrors
        [InlineData("MSB1234", "MSB1234", "MSB1234", "MSB4181", true, false)]// Log MSB1234, BuildWarningsAsMessages takes priority
        public void WarningsAsErrorsAndMessages_Tests(string WarningsAsMessages,
                                                      string WarningsAsErrors,
                                                      string WarningToLog,
                                                      string LogShouldContain,
                                                      bool allWarningsAreErrors = false,
                                                      bool useMSPrefix = true)
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                var prefix = useMSPrefix ? "MSBuild" : "";
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <PropertyGroup>
                        <{prefix}TreatWarningsAsErrors>{allWarningsAreErrors}</{prefix}TreatWarningsAsErrors>
                        <{prefix}WarningsAsMessages>{WarningsAsMessages}</{prefix}WarningsAsMessages>
                        <{prefix}WarningsAsErrors>{WarningsAsErrors}</{prefix}WarningsAsErrors>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""true"" ReturnHasLoggedErrors=""true"" WarningCode=""{WarningToLog}""/>
                        <ReturnFailureWithoutLoggingErrorTask/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(0);
                logger.ErrorCount.ShouldBe(1);

                logger.AssertLogContains(LogShouldContain);
            }
        }

        [Theory]

        [InlineData(true)]// Log MSB1234, BuildWarningsNotAsErrors takes priority
        [InlineData(false)]
        public void WarningsNotAsErrorsAndMessages_Tests(bool useMSPrefix)
        {
            string Warning = "MSB1235";
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                string prefix = useMSPrefix ? "MSBuild" : "";
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <PropertyGroup>
                        <{prefix}TreatWarningsAsErrors>true</{prefix}TreatWarningsAsErrors>
                        <{prefix}WarningsNotAsErrors>{Warning}</{prefix}WarningsNotAsErrors>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Warning Text=""some random text"" Code='{Warning}' />
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectSuccess();

                logger.WarningCount.ShouldBe(1);
                logger.ErrorCount.ShouldBe(0);

                logger.AssertLogContains(Warning);
            }
        }



        [Theory]
        [InlineData("TreatWarningsAsErrors", "true", false)] // All warnings are treated as errors
        [InlineData("WarningsAsErrors", "MSB1007", false)]
        [InlineData("WarningsAsMessages", "MSB1007", false)]
        [InlineData("WarningsNotAsErrors", "MSB1007", true)]
        [InlineData("WarningsNotAsErrors", "MSB1007", false)]
        public void WarningsChangeWaveTest(string property, string propertyData, bool treatWarningsAsErrors)
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                string warningCode = "MSB1007";
                string treatWarningsAsErrorsCodeProperty = treatWarningsAsErrors ? "<MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>" : "";
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave17_14.ToString());
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <PropertyGroup>
                        {treatWarningsAsErrorsCodeProperty}
                        <{property}>{propertyData}</{property}>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Warning Text=""some random text"" Code='{warningCode}' />
                    </Target>
                </Project>");
                if (treatWarningsAsErrors)
                {
                    // Since the "no prefix" variations can't do anything with the change wave disabled, this should always fail.
                    MockLogger logger = proj.BuildProjectExpectFailure();
                    logger.ErrorCount.ShouldBe(1);
                    logger.AssertLogContains(warningCode);
                }
                else
                {
                    MockLogger logger = proj.BuildProjectExpectSuccess();

                    logger.WarningCount.ShouldBe(1);
                    logger.ErrorCount.ShouldBe(0);

                    logger.AssertLogContains(warningCode);
                }
                ChangeWaves.ResetStateForTests();
            }
        }

        /// <summary>
        /// Item1 and Item2 log warnings and continue, item 3 logs a warn-> error and prevents item 4 from running in the batched build.
        /// </summary>
        [Fact]
        public void TaskLogsWarningAsError_BatchedBuild()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <PropertyGroup>
                        <MSBuildWarningsAsErrors>MSB1234</MSBuildWarningsAsErrors>
                    </PropertyGroup>
                    <ItemGroup>
                        <SomeItem Include=""Item1"">
                            <Return>true</Return>
                            <ReturnHasLoggedErrors>true</ReturnHasLoggedErrors>
                            <WarningCode>MSB1235</WarningCode>
                        </SomeItem>
                        <SomeItem Include=""Item2"">
                            <Return>true</Return>
                            <ReturnHasLoggedErrors>true</ReturnHasLoggedErrors>
                            <WarningCode>MSB1236</WarningCode>
                        </SomeItem>
                        <SomeItem Include=""Item3"">
                            <Return>true</Return>
                            <ReturnHasLoggedErrors>true</ReturnHasLoggedErrors>
                            <WarningCode>MSB1234</WarningCode>
                        </SomeItem>
                        <SomeItem Include=""Item4"">
                            <Return>true</Return>
                            <ReturnHasLoggedErrors>true</ReturnHasLoggedErrors>
                            <WarningCode>MSB1237</WarningCode>
                        </SomeItem>
                    </ItemGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Sources=""@(SomeItem)"" Return=""true"" ReturnHasLoggedErrors=""true"" WarningCode=""%(WarningCode)""/>
                        <ReturnFailureWithoutLoggingErrorTask/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(2);
                logger.ErrorCount.ShouldBe(1);

                // The build should STOP when a task logs an error, make sure ReturnFailureWithoutLoggingErrorTask doesn't run.
                logger.AssertLogDoesntContain("MSB1237");
            }
        }

        /// <summary>
        /// Task logs MSB1234 as a warning and returns true.
        /// Test behavior with MSBuildWarningsAsErrors & MSBuildTreatWarningsAsErrors
        /// Both builds should continue despite logging errors.
        /// </summary>
        [Theory]
        [InlineData("MSB1234", false, 1, 1)]
        [InlineData("MSB0000", true, 0, 2)]
        [InlineData("MSB1234", false, 1, 1, false)]
        [InlineData("MSB0000", true, 0, 2, false)]
        public void TaskReturnsTrue_Tests(string warningsAsErrors, bool treatAllWarningsAsErrors, int warningCountShouldBe, int errorCountShouldBe, bool useMSPrefix = true)
        {
            string prefix = useMSPrefix ? "MSBuild" : "";
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <PropertyGroup>
                        <{prefix}TreatWarningsAsErrors>{treatAllWarningsAsErrors}</{prefix}TreatWarningsAsErrors>
                        <{prefix}WarningsAsErrors>{warningsAsErrors}</{prefix}WarningsAsErrors>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1234""/>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1235""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(warningCountShouldBe);
                logger.ErrorCount.ShouldBe(errorCountShouldBe);

                // The build will continue so we should see the warning MSB1235
                logger.AssertLogContains("MSB1235");
            }
        }

        [Fact]
        public void TaskReturnsFailureButDoesNotLogError_ShouldCauseBuildFailure()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <ReturnFailureWithoutLoggingErrorTask/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.AssertLogContains("MSB4181");
            }
        }

        /// <summary>
        /// Test that a task that returns false without logging anything reports MSB4181 as a warning.
        /// </summary>
        [Fact]
        public void TaskReturnsFailureButDoesNotLogError_ContinueOnError_WarnAndContinue()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <ReturnFailureWithoutLoggingErrorTask
                            ContinueOnError=""WarnAndContinue""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectSuccess();

                logger.WarningCount.ShouldBe(1);

                logger.AssertLogContains("MSB4181");
            }
        }

        /// <summary>
        /// Test that a task that returns false after logging an error->warning does NOT also log MSB4181
        /// </summary>
        [Fact]
        public void TaskReturnsFailureAndLogsError_ContinueOnError_WarnAndContinue()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""false"" ErrorCode=""MSB1234"" ContinueOnError=""WarnAndContinue""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectSuccess();

                // The only warning should be the error->warning logged by the task.
                logger.WarningCount.ShouldBe(1);
                logger.AssertLogContains("MSB1234");
            }
        }

        [Fact]
        public void TaskReturnsFailureButDoesNotLogError_ContinueOnError_True()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <ReturnFailureWithoutLoggingErrorTask
                            ContinueOnError=""true""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectSuccess();

                logger.AssertLogContains("MSB4181");
            }
        }

        [Fact]
        public void TaskReturnsFailureButDoesNotLogError_ContinueOnError_ErrorAndStop()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <ReturnFailureWithoutLoggingErrorTask
                            ContinueOnError=""ErrorAndStop""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.AssertLogContains("MSB4181");
            }
        }

        [Fact]
        public void TaskReturnsFailureButDoesNotLogError_ContinueOnError_False()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <Target Name='Build'>
                        <ReturnFailureWithoutLoggingErrorTask
                            ContinueOnError=""false""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.AssertLogContains("MSB4181");
            }
        }
    }
}
