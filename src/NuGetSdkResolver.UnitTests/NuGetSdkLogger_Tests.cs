// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.MSBuildSdkResolver.UnitTests
{
    /// <summary>
    /// Tests for LazyFormattedEventArgs
    /// </summary>
    public class NuGetSdkLogger_Tests
    {
        [Fact]
        public void LogDebugMapsToMessage()
        {
            const string expectedMessage = "F4E2857F8B6F4ADC8D9B727DDBEE769B";

            VerifyLog(sdkLogger => sdkLogger.LogDebug(expectedMessage), expectedMessage, MessageImportance.Low, isWarning: false, isError: false);
        }

        [Fact]
        public void LogErrorMapsToError()
        {
            const string expectedMessage = "FC168C5B9E9C4FC199974BE664F5D723";

            VerifyLog(sdkLogger => sdkLogger.LogError(expectedMessage), expectedMessage, expectedMessageImportance: null, isWarning: false, isError: true);
        }

        [Fact]
        public void LogInformationMapsToMessage()
        {
            const string expectedMessage = "67170559A4EC47FE88FCC3E8B68E3522";

            VerifyLog(sdkLogger => sdkLogger.LogInformation(expectedMessage), expectedMessage, MessageImportance.Low, isWarning: false, isError: false);
        }

        [Fact]
        public void LogInformationSummaryMapsToMessage()
        {
            const string expectedMessage = "EA9F5D816A0342E38A4A87DB955ABC33";

            VerifyLog(sdkLogger => sdkLogger.LogInformationSummary(expectedMessage), expectedMessage, MessageImportance.Low, isWarning: false, isError: false);
        }

        [Theory]
        [InlineData(LogLevel.Debug, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Information, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Minimal, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Verbose, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Error, null, false, true)]
        [InlineData(LogLevel.Warning, null, true, false)]
        [InlineData(999, null, false, false)]
        public void LogLevelMapsToMessageWarningOrError(LogLevel logLevel, MessageImportance? expectedMessageImportance = null, bool isWarning = false, bool isError = false)
        {
            const string expectedMessage = "BE0F702B91714CED9AAE850CE1798430";

            VerifyLog(sdkLogger => sdkLogger.Log(logLevel, expectedMessage), expectedMessage, expectedMessageImportance, isWarning, isError);
        }

        [Theory]
        [InlineData(LogLevel.Debug, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Information, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Minimal, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Verbose, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Error, null, false, true)]
        [InlineData(LogLevel.Warning, null, true, false)]
        [InlineData(999, null, false, false)]
        public void LogLevelMapsToMessageWarningOrErrorAsync(LogLevel logLevel, MessageImportance? expectedMessageImportance = null, bool isWarning = false, bool isError = false)
        {
            const string expectedMessage = "BE0F702B91714CED9AAE850CE1798430";

            VerifyLog(async sdkLogger => await sdkLogger.LogAsync(logLevel, expectedMessage).ConfigureAwait(false), expectedMessage, expectedMessageImportance, isWarning, isError);
        }

        [Theory]
        [InlineData(LogLevel.Debug, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Information, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Minimal, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Verbose, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Error, null, false, true)]
        [InlineData(LogLevel.Warning, null, true, false)]
        [InlineData(999, null, false, false)]
        public void LogMessageMapsToMessageWarningOrError(LogLevel logLevel, MessageImportance? expectedMessageImportance = null, bool isWarning = false, bool isError = false)
        {
            const string expectedMessage = "B8F887DBCA4A4748824E9ED3CAC484A0";

            ILogMessage logMessage = new LogMessage(logLevel, expectedMessage);

            VerifyLog(sdkLogger => sdkLogger.Log(logMessage), expectedMessage, expectedMessageImportance, isWarning, isError);
        }

        [Theory]
        [InlineData(LogLevel.Debug, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Information, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Minimal, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Verbose, MessageImportance.Low, false, false)]
        [InlineData(LogLevel.Error, null, false, true)]
        [InlineData(LogLevel.Warning, null, true, false)]
        [InlineData(999, null, false, false)]
        public void LogMessageMapsToMessageWarningOrErrorAsync(LogLevel logLevel, MessageImportance? expectedMessageImportance = null, bool isWarning = false, bool isError = false)
        {
            const string expectedMessage = "5022EC7B7A694D41BFF1A6ED973297A4";

            ILogMessage logMessage = new LogMessage(logLevel, expectedMessage);

            VerifyLog(async sdkLogger => await sdkLogger.LogAsync(logMessage).ConfigureAwait(false), expectedMessage, expectedMessageImportance, isWarning, isError);
        }

        [Fact]
        public void LogMinimalSummaryMapsToMessage()
        {
            const string expectedMessage = "D6412F6087CE41C4803AD940E26E221B";

            VerifyLog(sdkLogger => sdkLogger.LogMinimal(expectedMessage), expectedMessage, MessageImportance.Low, isWarning: false, isError: false);
        }

        [Fact]
        public void LogVerboseMapsToMessage()
        {
            const string expectedMessage = "815F49653DB74CD6BD2B66201BB3BCA8";

            VerifyLog(sdkLogger => sdkLogger.LogVerbose(expectedMessage), expectedMessage, MessageImportance.Low, isWarning: false, isError: false);
        }

        [Fact]
        public void LogWarningMapsToWarning()
        {
            const string expectedMessage = "787607F4D1B141F898CCB432B5CB8CDE";

            VerifyLog(sdkLogger => sdkLogger.LogWarning(expectedMessage), expectedMessage, expectedMessageImportance: null, isWarning: true, isError: false);
        }

        private void VerifyLog(Action<NuGetSdkLogger> action, string expectedMessage, MessageImportance? expectedMessageImportance, bool isWarning, bool isError)
        {
            MockSdkLogger mockLogger = new MockSdkLogger();

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            NuGetSdkLogger sdkLogger = new NuGetSdkLogger(mockLogger, warnings, errors);

            action(sdkLogger);

            if (expectedMessageImportance.HasValue)
            {
                KeyValuePair<string, MessageImportance> item = mockLogger.LoggedMessages.ShouldHaveSingleItem();

                item.Key.ShouldBe(expectedMessage);
                item.Value.ShouldBe(expectedMessageImportance.Value);
            }
            else
            {
                mockLogger.LoggedMessages.ShouldBeEmpty();
            }

            if (isWarning)
            {
                warnings.ShouldHaveSingleItem().ShouldBe(expectedMessage);
            }
            else
            {
                warnings.ShouldBeEmpty();
            }

            if (isError)
            {
                errors.ShouldHaveSingleItem().ShouldBe(expectedMessage);
            }
            else
            {
                errors.ShouldBeEmpty();
            }
        }
    }
}
