using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public sealed class WarningsAsMessagesAndErrorsTests
    {
        private const string ExpectedEventMessage = "03767942CDB147B98D0ECDBDE1436DA3";
        private const string ExpectedEventCode = "0BF68998";

        ITestOutputHelper _output;

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

        /// <summary>
        /// https://github.com/Microsoft/msbuild/issues/2667
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

                MockLogger logger = project1.BuildProjectExpectFailure();

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
        public void TreatWarningsAsErrorsWhenSpecifiedThroughAdditiveProperty()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", "123"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", $@"$(MSBuildWarningsAsErrors);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsErrors", "$(MSBuildWarningsAsErrors);ABC")
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
        /// https://github.com/Microsoft/msbuild/issues/2667
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

                MockLogger logger = project1.BuildProjectExpectSuccess();

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
                    warningsAsMessages: "$(Foo)"
                )
            );

            VerifyBuildMessageEvent(logger);
        }

        [Fact]
        public void TreatWarningsAsMessagesWhenSpecifiedThroughAdditiveProperty()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    customProperties: new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "123"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", $@"$(MSBuildWarningsAsMessages);
                                                                                       {ExpectedEventCode.ToLowerInvariant()}"),
                        new KeyValuePair<string, string>("MSBuildWarningsAsMessages", "$(MSBuildWarningsAsMessages);ABC")
                    }));

            VerifyBuildMessageEvent(logger);
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
                    }));

            VerifyBuildWarningEvent(logger);
        }

        [Fact]
        public void TreatWarningAsMessageOverridesTreatingItAsError()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(
                GetTestProject(
                    warningsAsMessages: ExpectedEventCode,
                    warningsAsErrors: ExpectedEventCode
                    ));

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

        /// <summary>
        /// We have a unique task host per bucket. Show that in these scenarios the build will stop if one sees an error.
        /// </summary>
        [Fact]
        public void TaskReturnsHasLoggedErrorAndLogsWarningAsError_BuildShouldStopAndFail_BatchedBuild()
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
                            <WarningCode>MSB1234</WarningCode>
                        </SomeItem>
                        <SomeItem Include=""Item2"">
                            <Return>true</Return>
                            <ReturnHasLoggedErrors>true</ReturnHasLoggedErrors>
                            <WarningCode>MSB1235</WarningCode>
                        </SomeItem>
                    </ItemGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Sources=""@(SomeItem)"" Return=""true"" ReturnHasLoggedErrors=""true"" WarningCode=""%(WarningCode)""/>
                        <ReturnFailureWithoutLoggingErrorTask/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(0);
                logger.ErrorCount.ShouldBe(1);

                // The build should STOP when a task logs an error, make sure ReturnFailureWithoutLoggingErrorTask doesn't run. 
                logger.AssertLogDoesntContain("MSB4181");
            }
        }

        /// <summary>
        /// Item1 and Item2 log warnings and continue, item 3 logs a warn-> error and prevents item 4 from running in the batched build.
        /// </summary>
        [Fact]
        public void TaskReturnsHasLoggedErrorAndLogsWarningAsError_BuildShouldStopOnceItLogsWarningAsErrorAndFail_BatchedBuild()
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

        [Fact]
        public void TaskReturnsHasLoggedErrorAndLogsWarningAsError_BuildShouldFinishAndFail()
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
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""true"" ReturnHasLoggedErrors=""true"" WarningCode=""MSB1234""/>
                        <ReturnFailureWithoutLoggingErrorTask/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(0);
                logger.ErrorCount.ShouldBe(1);

                // The build should STOP when a task logs an error, make sure ReturnFailureWithoutLoggingErrorTask doesn't run. 
                logger.AssertLogDoesntContain("MSB4181");
            }
        }

        /// <summary>
        /// MSBuild behavior as of 16.10: As long as a task returns true, the build will continue despite logging a warning as error.
        /// This tests MSBuildWarningsAsErrors
        /// </summary>
        [Fact]
        public void TaskReturnsTrueButLogsWarningAsError_BuildShouldFinishAndFail()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""LogWarningReturnHasLoggedError"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <PropertyGroup>
                        <MSBuildWarningsAsErrors>MSB1234</MSBuildWarningsAsErrors>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1234""/>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1235""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(1);
                logger.ErrorCount.ShouldBe(1);

                // The build will continue so we should see the warning MSB1235
                logger.AssertLogContains("MSB1235");
            }
        }


        /// <summary>
        /// MSBuild behavior as of 16.10: As long as a task returns true, the build will continue despite logging warning as error.
        /// This test specifically tests the MSBuildTreatWarningsAsErrors flag as opposed to MSBuildWarningsAsErrors
        /// </summary>
        [Fact]
        public void TaskReturnsTrueButLogsWarning_TreatWarningsAsErrors_BuildShouldFinishAndFail()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                TransientTestProjectWithFiles proj = env.CreateTestProjectWithFiles($@"
                <Project>
                    <UsingTask TaskName = ""ReturnFailureWithoutLoggingErrorTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""CustomLogAndReturnTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <UsingTask TaskName = ""LogWarningReturnHasLoggedError"" AssemblyName=""Microsoft.Build.Engine.UnitTests""/>
                    <PropertyGroup>
                        <MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1234""/>
                        <CustomLogAndReturnTask Return=""true"" WarningCode=""MSB1235""/>
                    </Target>
                </Project>");

                MockLogger logger = proj.BuildProjectExpectFailure();

                logger.WarningCount.ShouldBe(0);
                logger.ErrorCount.ShouldBe(2);

                // The build will continue so we should see the error MSB1235
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
