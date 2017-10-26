using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{
    public sealed class WarningsAsMessagesAndErrorsTests
    {
        private const string ExpectedEventMessage = "03767942CDB147B98D0ECDBDE1436DA3";
        private const string ExpectedEventCode = "0BF68998";

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
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
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
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
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
    }
}
