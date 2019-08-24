// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Microsoft.Build.Execution;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using MockHost = Microsoft.Build.UnitTests.BackEnd.MockHost;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Contain the logging services tests which deal with the logging methods themselves
    /// </summary>
    public class LoggingServicesLogMethod_Tests
    {
        #region Data
        /// <summary>
        /// A generic valid build event context which can be used in the tests.
        /// </summary>
        private static BuildEventContext s_buildEventContext = new BuildEventContext(1, 2, BuildEventContext.InvalidProjectContextId, 4);

        /// <summary>
        /// buildevent context for target events, note the invalid taskId, target started and finished events have this.
        /// </summary>
        private static BuildEventContext s_targetBuildEventContext = new BuildEventContext(1, 2, BuildEventContext.InvalidProjectContextId, -1);
        #endregion

        #region Event based logging method tests

        /// <summary>
        /// Make sure an InternalErrorExcetpionis thrown when a null event is attempted to be logged
        /// </summary>
        [Fact]
        public void LogBuildEventNullEvent()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                LoggingService loggingService = (LoggingService)LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
                loggingService.LogBuildEvent(null);
            }
           );
        }
        /// <summary>
        /// Test LogBuildevent by logging a number of events with both OnlyLogCriticalEvents On and Off
        /// </summary>
        [Fact]
        public void LogBuildEvents()
        {
            // This event should only be logged when OnlyLogCriticalEvents is off
            BuildMessageEventArgs messageEvent = new BuildMessageEventArgs("MyMessage", "HelpKeyword", "Sender", MessageImportance.High);

            // These three should be logged when OnlyLogCritical Events is on or off
            BuildWarningEventArgs warning = new BuildWarningEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            BuildErrorEventArgs error = new BuildErrorEventArgs("SubCategoryForSchemaValidationErrors", "MSB4000", "file", 1, 2, 3, 4, "message", "help", "sender");
            ExternalProjectStartedEventArgs externalStartedEvent = new ExternalProjectStartedEventArgs("message", "help", "senderName", "projectFile", "targetNames");

            ProcessBuildEventHelper loggingService = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            // Verify when OnlyLogCriticalEvents is false
            LogandVerifyBuildEvent(messageEvent, loggingService);
            LogandVerifyBuildEvent(warning, loggingService);
            LogandVerifyBuildEvent(error, loggingService);
            LogandVerifyBuildEvent(externalStartedEvent, loggingService);

            // Verify when OnlyLogCriticalEvents is true
            loggingService.OnlyLogCriticalEvents = true;
            loggingService.LogBuildEvent(messageEvent);
            Assert.Null(loggingService.ProcessedBuildEvent); // "Expected ProcessedBuildEvent to be null"
            LogandVerifyBuildEvent(warning, loggingService);
            LogandVerifyBuildEvent(error, loggingService);
            LogandVerifyBuildEvent(externalStartedEvent, loggingService);
        }

        #endregion

        #region TestErrors

        #region LogError

        /// <summary>
        /// Verify an InternalErrorException is thrown when MessageResourceName  is null.
        /// </summary>
        [Fact]
        public void LogErrorNullMessageResource()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogError(s_buildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo("foo.cs"), null, "MyTask");
            }
           );
        }
        /// <summary>
        /// Verify an InternlErrorException is thrown when an empty MessageResourceName is passed in.
        /// </summary>
        [Fact]
        public void LogErrorEmptyMessageResource()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogError(s_buildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo("foo.cs"), string.Empty, "MyTask");
            }
           );
        }
        /// <summary>
        /// Verify a message is logged when all of the parameters are filled out correctly.
        /// </summary>
        [Fact]
        public void LogErrorGoodParameters()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string errorCode;
            string helpKeyword;
            string taskName = "TaskName";
            string subcategoryKey = "SubCategoryForSolutionParsingErrors";
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out errorCode, out helpKeyword, "FatalTaskError", taskName);
            string subcategory = AssemblyResources.GetString(subcategoryKey);

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            service.LogError(s_buildEventContext, subcategoryKey, fileInfo, "FatalTaskError", taskName);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, subcategory);
        }

        #endregion

        #region LogInvalidProjectFileError

        /// <summary>
        /// Verify an exception is thrown when a null buildevent context is passed in
        /// </summary>
        [Fact]
        public void LogInvalidProjectFileErrorNullEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogInvalidProjectFileError(null, new InvalidProjectFileException());
            }
           );
        }
        /// <summary>
        /// Verify an exception is thrown when a null Invalid ProjectFile exception is passed in
        /// </summary>
        [Fact]
        public void LogInvalidProjectFileErrorNullException()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogInvalidProjectFileError(s_buildEventContext, null);
            }
           );
        }
        /// <summary>
        /// Verify a message is logged when both parameters are good and
        /// the exception has not been logged yet. Verify with and without OnlyLogCriticalEvents.
        /// In Both cases we expect the event to be logged
        /// </summary>
        [Fact]
        public void LogInvalidProjectFileError()
        {
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            InvalidProjectFileException exception = new InvalidProjectFileException("ProjectFile", 1, 2, 3, 4, "Message", "errorSubCategory", "ErrorCode", "HelpKeyword");

            // Log the exception for the first time
            Assert.False(exception.HasBeenLogged);
            service.LogInvalidProjectFileError(s_buildEventContext, exception);
            Assert.True(exception.HasBeenLogged);
            BuildEventFileInfo fileInfo = new BuildEventFileInfo(exception.ProjectFile, exception.LineNumber, exception.ColumnNumber, exception.EndLineNumber, exception.EndColumnNumber);
            VerifyBuildErrorEventArgs(fileInfo, exception.ErrorCode, exception.HelpKeyword, exception.BaseMessage, service, exception.ErrorSubcategory);

            // Verify when the exception is logged again that it does not actually get logged due to it already being logged
            service.ResetProcessedBuildEvent();
            service.LogInvalidProjectFileError(s_buildEventContext, exception);
            Assert.Null(service.ProcessedBuildEvent);

            // Reset the HasLogged field and verify OnlyLogCriticalEvents does not effect the logging of the message
            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            exception.HasBeenLogged = false;
            service.LogInvalidProjectFileError(s_buildEventContext, exception);
            VerifyBuildErrorEventArgs(fileInfo, exception.ErrorCode, exception.HelpKeyword, exception.BaseMessage, service, exception.ErrorSubcategory);
        }

        #endregion

        #region LogFatalError

        /// <summary>
        /// Verify an InternalErrorException is thrown when a null build event context is passed in
        /// </summary>
        [Fact]
        public void LogFatalErrorNullContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogFatalError(null, new Exception("SuperException"), new BuildEventFileInfo("foo.cs"), "FatalTaskError", "TaskName");
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when fileInfo is null
        /// </summary>
        [Fact]
        public void LogFatalErrorNullFileInfo()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogFatalError(s_buildEventContext, new Exception("SuperException"), null, "FatalTaskError", "TaskName");
            }
           );
        }
        /// <summary>
        /// Verify a error message is correctly logged when  the exception is null.
        /// </summary>
        [Fact]
        public void LogFatalErrorNullException()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string errorCode;
            string helpKeyword;
            string resourceName = "FatalTaskError";
            string parameters = "TaskName";
            string message = null;

            GenerateMessageFromExceptionAndResource(null, resourceName, out errorCode, out helpKeyword, out message, parameters);
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogFatalError(s_buildEventContext, null, fileInfo, resourceName, parameters);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, null);
        }

        /// <summary>
        /// Verify an InternalErrorException is thrown when messageResourceName is null
        /// </summary>
        [Fact]
        public void LogFatalErrorNullMessageResourceName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogFatalError(s_buildEventContext, new Exception("SuperException"), fileInfo, null);
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when messageResourceName is empty
        /// </summary>
        [Fact]
        public void LogFatalErrorEmptyMessageResourceName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogFatalError(s_buildEventContext, new Exception("SuperException"), fileInfo, string.Empty, null);
            }
           );
        }
        /// <summary>
        /// Verify a error message is correctly logged when all of the inputs are valid.
        /// </summary>
        [Fact]
        public void LogFatalErrorAllGoodInput()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            Exception exception = new Exception("SuperException");
            string resourceName = "FatalTaskError";
            string parameter = "TaskName";
            string errorCode;
            string helpKeyword;
            string message;
            GenerateMessageFromExceptionAndResource(exception, resourceName, out errorCode, out helpKeyword, out message, parameter);

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            service.LogFatalError(s_buildEventContext, exception, fileInfo, resourceName, parameter);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, null);
        }
        #endregion

        #region LogFatalBuildError

        /// <summary>
        /// Verify a error message is correctly logged when all of the inputs are valid.
        /// </summary>
        [Fact]
        public void LogFatalBuildErrorGoodInput()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            Exception exception = new Exception("SuperException");
            string resourceName = "FatalBuildError";
            string errorCode;
            string helpKeyword;
            string message;
            GenerateMessageFromExceptionAndResource(exception, resourceName, out errorCode, out helpKeyword, out message);

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogFatalBuildError(s_buildEventContext, exception, fileInfo);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, null);
        }
        #endregion

        #region LogFatalTaskError

        /// <summary>
        /// Verify an InternalErrorException is thrown when taskName is null
        /// </summary>
        [Fact]
        public void LogFatalTaskErrorNullTaskNameName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogFatalTaskError(s_buildEventContext, new Exception("SuperException"), fileInfo, null);
            }
           );
        }
        /// <summary>
        /// Verify a error message is correctly logged when all of the inputs are valid.
        /// </summary>
        [Fact]
        public void LogFatalTaskError()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            Exception exception = new Exception("SuperException");
            string errorCode;
            string helpKeyword;
            string resourceName = "FatalTaskError";
            string parameters = "TaskName";
            string message = null;

            GenerateMessageFromExceptionAndResource(exception, resourceName, out errorCode, out helpKeyword, out message, parameters);
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogFatalTaskError(s_buildEventContext, exception, fileInfo, parameters);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, null);

            // Test when the task name is empty
            GenerateMessageFromExceptionAndResource(exception, resourceName, out errorCode, out helpKeyword, out message, String.Empty);
            service.ResetProcessedBuildEvent();
            service.LogFatalTaskError(s_buildEventContext, exception, fileInfo, string.Empty);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, null);
        }
        #endregion

        #region LogErrorFromText
        /// <summary>
        /// Verify an InternalErrorException is thrown when buildEventContext is null.
        /// </summary>
        [Fact]
        public void LogErrorFromTextNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogErrorFromText(null, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", new BuildEventFileInfo("foo.cs"), "Message");
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException when a null FileInfo is passed in
        /// </summary>
        [Fact]
        public void LogErrorFromTextNullFileInfo()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogErrorFromText(s_buildEventContext, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", null, "Message");
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when a null message is passed in
        /// </summary>
        [Fact]
        public void LogErrorFromTextNullMessage()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogErrorFromText(null, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", new BuildEventFileInfo("foo.cs"), null);
            }
           );
        }
        /// <summary>
        /// Test LogErrorFromText with a number of different inputs
        /// </summary>
        [Fact]
        public void LogErrorFromTextTests()
        {
            string warningCode;
            string helpKeyword;
            string subcategoryKey = "SubCategoryForSolutionParsingErrors";
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out warningCode, out helpKeyword, "FatalTaskError", "MyTask");

            // Test ErrorCode
            TestLogErrorFromText(null, helpKeyword, subcategoryKey, message);
            TestLogErrorFromText(String.Empty, helpKeyword, subcategoryKey, message);

            // Test HelpKeyword
            TestLogErrorFromText(warningCode, null, subcategoryKey, message);
            TestLogErrorFromText(warningCode, String.Empty, subcategoryKey, message);

            // Test subcategory (we use the key, the actual one is generated in TestLogFromText
            TestLogErrorFromText(warningCode, helpKeyword, null, message);

            // Test empty message
            TestLogErrorFromText(warningCode, helpKeyword, subcategoryKey, String.Empty);

            // Test Good
            TestLogErrorFromText(warningCode, helpKeyword, subcategoryKey, message);
        }

        /// <summary>
        /// Make sure if an imported project has an invalid project file exception say by trying to run a nonexistent task that we properly get
        /// the [projectfile] post fix information.
        /// </summary>
        [Fact]
        public void VerifyErrorPostfixForInvalidProjectFileException()
        {
            MockLogger mockLogger = new MockLogger();
            string tempPath = Path.GetTempPath();
            string testTempPath = Path.Combine(tempPath, "VerifyErrorPostfixForInvalidProjectFileException");
            string projectFile = Path.Combine(testTempPath, "a.proj");
            string targetsFile = Path.Combine(testTempPath, "x.targets");
            string projectfileContent =
                @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='x.targets'/>
                    </Project>
                ";

            string targetsfileContent = @"
                 <Project DefaultTargets='Build' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                   <UsingTask TaskName='RandomTask' AssemblyName='NotARealTaskLocaiton'/>    
                   <Target Name='Build'>
                       <RandomTask/>
                   </Target>
                 </Project>
                ";
            try
            {
                Directory.CreateDirectory(testTempPath);
                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectfileContent)));
                project.Save(projectFile);
                project = ProjectRootElement.Create(XmlReader.Create(new StringReader(targetsfileContent)));
                project.Save(targetsFile);
                Project msbuildProject = new Project(projectFile);
                msbuildProject.Build(mockLogger);

                List<BuildErrorEventArgs> errors = mockLogger.Errors;
                Assert.Single(errors);
                BuildErrorEventArgs error = errors[0];
                Assert.Equal(targetsFile, error.File);
                Assert.Equal(projectFile, error.ProjectFile);
            }
            finally
            {
                if (Directory.Exists(testTempPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testTempPath, true);
                }

                if (File.Exists(targetsFile))
                {
                    File.Delete(targetsFile);
                }

                if (File.Exists(projectFile))
                {
                    File.Delete(projectFile);
                }
            }
        }
        #endregion
        #endregion

        #region TestWarnings
        #region Test LogTaskWarningFromException
        /// <summary>
        /// Verify an InternalErrorException is thrown when taskName is null
        /// </summary>
        [Fact]
        public void LogTaskWarningFromExceptionNullTaskName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTaskWarningFromException(s_buildEventContext, null, fileInfo, null);
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when taskName is empty
        /// </summary>
        [Fact]
        public void LogTaskWarningFromExceptionEmptyTaskName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTaskWarningFromException(s_buildEventContext, null, fileInfo, null);
            }
           );
        }
        /// <summary>
        /// Verify a LogTaskWarningFromException with a null exception and a non null exception
        /// with all of the other fields properly filled out.
        /// </summary>
        [Fact]
        public void LogTaskWarningFromException()
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string resourceName = "FatalTaskError";
            string parameters = "TaskName";
            string warningCode;
            string helpKeyword;
            string message;
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            // Check with a null exception
            GenerateMessageFromExceptionAndResource(null, resourceName, out warningCode, out helpKeyword, out message, parameters);
            service.LogTaskWarningFromException(s_buildEventContext, null, fileInfo, parameters);
            VerifyBuildWarningEventArgs(fileInfo, warningCode, helpKeyword, message, service, null);

            // Check when the exception is not null
            service.ResetProcessedBuildEvent();
            Exception exception = new Exception("SuperException");
            GenerateMessageFromExceptionAndResource(exception, resourceName, out warningCode, out helpKeyword, out message, parameters);
            service.LogTaskWarningFromException(s_buildEventContext, exception, fileInfo, parameters);
            VerifyBuildWarningEventArgs(fileInfo, warningCode, helpKeyword, message, service, null);
        }
        #endregion

        #region LogWarning
        /// <summary>
        /// Verify an exception is when a null MessageResourceName is passed in
        /// </summary>
        [Fact]
        public void LogWarningNullMessageResource()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogWarning(s_buildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo("foo.cs"), null, "MyTask");
            }
           );
        }
        /// <summary>
        /// Verify an exception is when a empty MessageResourceName is passed in.
        /// </summary>
        [Fact]
        public void LogWarningEmptyMessageResource()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogWarning(s_buildEventContext, "SubCategoryForSolutionParsingErrors", new BuildEventFileInfo("foo.cs"), string.Empty, "MyTask");
            }
           );
        }
        /// <summary>
        /// Verify a message is logged when all of the parameters are filled out
        /// </summary>
        [Fact]
        public void LogWarningTests()
        {
            TestLogWarning(null, "SubCategoryForSolutionParsingErrors");
            TestLogWarning(String.Empty, "SubCategoryForSolutionParsingErrors");
            TestLogWarning("MyTask", "SubCategoryForSolutionParsingErrors");
        }

        #endregion

        #region LogWarningErrorFromText
        /// <summary>
        /// Verify an InternalErrorException is thrown when a null buildEventContext is passed in
        /// </summary>
        [Fact]
        public void LogWarningFromTextNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogWarningFromText(null, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", new BuildEventFileInfo("foo.cs"), "Message");
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when a null fileInfo is passed in
        /// </summary>
        [Fact]
        public void LogWarningFromTextNullFileInfo()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogWarningFromText(s_buildEventContext, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", null, "Message");
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when a null message is passed in
        /// </summary>
        [Fact]
        public void LogWarningFromTextNullMessage()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogWarningFromText(null, "SubCategoryForSolutionParsingErrors", "WarningCode", "HelpKeyword", new BuildEventFileInfo("foo.cs"), null);
            }
           );
        }
        /// <summary>
        /// Test LogWarningFromText with a number of different inputs
        /// </summary>
        [Fact]
        public void LogWarningFromTextTests()
        {
            string warningCode;
            string helpKeyword;
            string subcategoryKey = "SubCategoryForSolutionParsingErrors";
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out warningCode, out helpKeyword, "FatalTaskError", "MyTask");

            TestLogWarningFromText(null, helpKeyword, subcategoryKey, message);
            TestLogWarningFromText(String.Empty, helpKeyword, subcategoryKey, message);

            TestLogWarningFromText(warningCode, null, subcategoryKey, message);
            TestLogWarningFromText(warningCode, String.Empty, subcategoryKey, message);

            TestLogWarningFromText(warningCode, null, subcategoryKey, message);
            TestLogWarningFromText(warningCode, String.Empty, subcategoryKey, message);

            TestLogWarningFromText(warningCode, helpKeyword, null, message);

            TestLogWarningFromText(warningCode, helpKeyword, subcategoryKey, String.Empty);
            TestLogWarningFromText(warningCode, helpKeyword, subcategoryKey, message);
        }

        #endregion
        #endregion

        #region LogCommentTests

        /// <summary>
        /// Verify an InternalErrorException is thrown when a null messageResource name is passed in
        /// </summary>
        [Fact]
        public void LogCommentNullMessageResourceName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogComment(s_buildEventContext, MessageImportance.Low, null, null);
            }
           );
        }
        /// <summary>
        /// Verify an InternalErrorException is thrown when a empty messageResource name is passed in
        /// </summary>
        [Fact]
        public void LogCommentEmptyMessageResourceName()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogComment(s_buildEventContext, MessageImportance.Low, String.Empty, null);
            }
           );
        }
        /// <summary>
        /// Verify LogComment by testing it with OnlyLogCriticalEvents On and Off when the rest of the fields are
        /// valid inputs.
        /// </summary>
        [Fact]
        public void LogCommentGoodMessage()
        {
            MessageImportance messageImportance = MessageImportance.Normal;
            string message = ResourceUtilities.GetResourceString("BuildFinishedSuccess");

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            // Verify message is logged when OnlyLogCriticalEvents is false
            service.LogComment(s_buildEventContext, messageImportance, "BuildFinishedSuccess");
            VerityBuildMessageEventArgs(service, messageImportance, message);

            // Verify no message is logged when OnlyLogCriticalEvents is true
            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogComment(s_buildEventContext, MessageImportance.Normal, "BuildFinishedSuccess");
            Assert.Null(service.ProcessedBuildEvent);
        }

        #endregion

        #region LogCommentFromTextTests

        /// <summary>
        /// Verify an InternalErrorException is thrown when a null message is passed in
        /// </summary>
        [Fact]
        public void LogCommentFromTextNullMessage()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogCommentFromText(s_buildEventContext, MessageImportance.Low, null);
            }
           );
        }
        /// <summary>
        /// Verify a message is logged when an empty message is passed in
        /// </summary>
        [Fact]
        public void LogCommentFromTextEmptyMessage()
        {
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogCommentFromText(s_buildEventContext, MessageImportance.Low, string.Empty);
        }

        /// <summary>
        /// Verify an InternalErrorException is thrown when a null build event context is passed in
        /// </summary>
        [Fact]
        public void LogCommentFromTextNullBuildEventContextMessage()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogCommentFromText(null, MessageImportance.Low, "Hello");
            }
           );
        }
        /// <summary>
        /// Make sure we can log a comment when everything should be working correctly
        /// </summary>
        [Fact]
        public void LogCommentFromTextGoodMessage()
        {
            MessageImportance messageImportance = MessageImportance.Normal;
            string message = ResourceUtilities.GetResourceString("BuildFinishedSuccess");

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogCommentFromText(s_buildEventContext, messageImportance, ResourceUtilities.GetResourceString("BuildFinishedSuccess"));
            VerityBuildMessageEventArgs(service, messageImportance, message);

            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogCommentFromText(s_buildEventContext, MessageImportance.Normal, ResourceUtilities.GetResourceString("BuildFinishedSuccess"));
            Assert.Null(service.ProcessedBuildEvent);
        }
        #endregion

        #region LogStatusMessages

        #region ProjectEvents

        #region ProjectStarted

        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void ProjectStartedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogProjectStarted(null, 1, 2, s_buildEventContext, "ProjectFile", "TargetNames", null, null);
            }
           );
        }
        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void ProjectStartedNullParentBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogProjectStarted(s_buildEventContext, 1, 2, null, "ProjectFile", "TargetNames", null, null);
            }
           );
        }
        /// <summary>
        /// Test the case where ProjectFile is good and TargetNames is null.
        /// Expect an event to be logged
        /// </summary>
        [Fact]
        public void ProjectStartedEventTests()
        {
            // Good project File and null target names
            LogProjectStartedTestHelper("ProjectFile", null);

            // Good project File and empty target names
            LogProjectStartedTestHelper("ProjectFile", string.Empty);

            // Null project file and null target names
            LogProjectStartedTestHelper(null, null);

            // Empty project file null target Names
            LogProjectStartedTestHelper(string.Empty, null);

            // Empty project File and Empty target Names
            LogProjectStartedTestHelper(string.Empty, string.Empty);

            // TestGoodInputs
            LogProjectStartedTestHelper("ProjectFile", "TargetNames");
        }

        #endregion

        #region ProjectFinished
        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void ProjectFinishedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogProjectFinished(null, "ProjectFile", true);
            }
           );
        }
        /// <summary>
        /// Test the project finished event
        /// </summary>
        [Fact]
        public void ProjectFinished()
        {
            TestProjectFinishedEvent(null, true);
            TestProjectFinishedEvent(String.Empty, true);
            TestProjectFinishedEvent("ProjectFile", true);
            TestProjectFinishedEvent("ProjectFile", false);
        }

        #endregion
        #endregion

        #region BuildStartedFinishedEvents

        /// <summary>
        /// Make sure we can log a build started event correctly.
        /// Test both the LogOnlyCriticalEvents true and false
        /// </summary>
        [Fact]
        public void LogBuildStarted()
        {
            ProcessBuildEventHelper service =
                (ProcessBuildEventHelper) ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            service.LogBuildStarted();

            BuildStartedEventArgs buildEvent =
                new BuildStartedEventArgs(
                    ResourceUtilities.GetResourceString("BuildStarted"),
                    null /* no help keyword */,
                    service.ProcessedBuildEvent.Timestamp);

            Assert.IsType<BuildStartedEventArgs>(service.ProcessedBuildEvent);
            Assert.Equal(buildEvent, (BuildStartedEventArgs) service.ProcessedBuildEvent,
                new EventArgsEqualityComparer<BuildStartedEventArgs>());
        }

        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/437")]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void LogBuildStartedCriticalOnly()
        {
            ProcessBuildEventHelper service =
                (ProcessBuildEventHelper) ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.OnlyLogCriticalEvents = true;
            service.LogBuildStarted();

            BuildStartedEventArgs buildEvent =
                new BuildStartedEventArgs(
                    string.Empty,
                    null /* no help keyword */);

            Assert.IsType<BuildStartedEventArgs>(service.ProcessedBuildEvent);
            Assert.Equal(buildEvent, (BuildStartedEventArgs) service.ProcessedBuildEvent,
                new EventArgsEqualityComparer<BuildStartedEventArgs>());
        }

        /// <summary>
        /// Make sure we can log a build finished event correctly.
        /// Verify the success cases as well as OnlyLogCriticalEvents
        /// </summary>
        [Fact]
        public void LogBuildFinished()
        {
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogBuildFinished(true);
            BuildFinishedEventArgs buildEvent = new BuildFinishedEventArgs(ResourceUtilities.GetResourceString("BuildFinishedSuccess"), null /* no help keyword */, true, service.ProcessedBuildEvent.Timestamp);
            Assert.True(((BuildFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildEvent));

            service.ResetProcessedBuildEvent();
            service.LogBuildFinished(false);
            buildEvent = new BuildFinishedEventArgs(ResourceUtilities.GetResourceString("BuildFinishedFailure"), null /* no help keyword */, false, service.ProcessedBuildEvent.Timestamp);
            Assert.True(((BuildFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildEvent));

            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogBuildFinished(true);
            buildEvent = new BuildFinishedEventArgs(string.Empty, null /* no help keyword */, true, service.ProcessedBuildEvent.Timestamp);
            Assert.True(((BuildFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildEvent));
        }

        /// <summary>
        ///  Exercise Asynchronous code path, this method should return right away as there are no events to process.
        ///  This will be further tested in the LoggingService_Tests class.
        /// </summary>
        [Fact]
        public void TestBuildFinishedWaitForEvents()
        {
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Asynchronous, 1);
            service.LogBuildFinished(true);
        }

        #endregion

        #region LogTaskEvents

        #region TaskStarted

        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void TaskStartedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTaskStarted(null, "MyTask", "ProjectFile", "ProjectFileOfTask");
            }
           );
        }
        /// <summary>
        /// Test the case where TaskName
        /// </summary>
        [Fact]
        public void TaskStartedEvent()
        {
            TestTaskStartedEvent(null, "ProjectFile", "ProjectFileOfTaskNode");
            TestTaskStartedEvent(String.Empty, "ProjectFile", "ProjectFileOfTaskNode");

            TestTaskStartedEvent("TaskName", null, "ProjectFileOfTaskNode");
            TestTaskStartedEvent("TaskName", String.Empty, "ProjectFileOfTaskNode");

            TestTaskStartedEvent("TaskName", "ProjectFile", null);
            TestTaskStartedEvent("TaskName", "ProjectFile", String.Empty);

            TestTaskStartedEvent("TaskName", "ProjectFile", "ProjectFileOfTaskNode");
        }

        #endregion

        #region TaskFinished
        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void TaskFinishedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTaskFinished(null, "MyTask", "ProjectFile", "ProjectFileOfTask", true);
            }
           );
        }
        /// <summary>
        /// Test the case where TaskName is null.
        /// </summary>
        [Fact]
        public void TaskFinishedNullTaskName()
        {
            TestTaskFinished(null, "ProjectFile", "ProjectFileOfTaskNode", true);
            TestTaskFinished(String.Empty, "ProjectFile", "ProjectFileOfTaskNode", true);

            TestTaskFinished("TaskName", null, "ProjectFileOfTaskNode", true);
            TestTaskFinished("TaskName", String.Empty, "ProjectFileOfTaskNode", true);

            TestTaskFinished("TaskName", "ProjectFile", null, true);
            TestTaskFinished("TaskName", "ProjectFile", String.Empty, true);

            TestTaskFinished("TaskName", "ProjectFile", "ProjectFileOfTaskNode", true);
            TestTaskFinished("TaskName", "ProjectFile", "ProjectFileOfTaskNode", false);
        }

        #endregion

        #endregion

        #region LogTargetEvents

        #region TargetStarted
        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void TargetStartedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTargetStarted(null, "MyTarget", "ProjectFile", "ProjectFileOfTarget", null, TargetBuiltReason.None);
            }
           );
        }
        /// <summary>
        /// Test the target started event with a null target name.
        /// </summary>
        [Fact]
        public void TargetStartedNullTargetName()
        {
            TestTargetStartedEvent(null, "ProjectFile", "projectFileOfTarget");
            TestTargetStartedEvent(String.Empty, "ProjectFile", "projectFileOfTarget");
            TestTargetStartedEvent("TargetName", null, "projectFileOfTarget");
            TestTargetStartedEvent("TargetName", String.Empty, "projectFileOfTarget");
            TestTargetStartedEvent("Good", "ProjectFile", null);
            TestTargetStartedEvent("Good", "ProjectFile", String.Empty);
            TestTargetStartedEvent("Good", "ProjectFile", "projectFileOfTarget");
            TestTargetStartedEvent("Good", "ProjectFile", "ProjectFile");
        }

        /// <summary>
        /// Test the target started event with different values being null.
        /// </summary>
        [Fact]
        public void TargetStartedWithParentTarget()
        {
            TestTargetStartedWithParentTargetEvent(null, "ProjectFile", "projectFileOfTarget");
            TestTargetStartedWithParentTargetEvent(String.Empty, "ProjectFile", "projectFileOfTarget");
            TestTargetStartedWithParentTargetEvent("TargetName", null, "projectFileOfTarget");
            TestTargetStartedWithParentTargetEvent("TargetName", String.Empty, "projectFileOfTarget");
            TestTargetStartedWithParentTargetEvent("Good", "ProjectFile", null);
            TestTargetStartedWithParentTargetEvent("Good", "ProjectFile", String.Empty);
            TestTargetStartedWithParentTargetEvent("Good", "ProjectFile", "projectFileOfTarget");
            TestTargetStartedWithParentTargetEvent("Good", "ProjectFile", "ProjectFile");
        }
        #endregion

        #region TargetFinished
        /// <summary>
        /// Expect an exception to be thrown if a null build event context is passed in
        /// and OnlyLogCriticalEvents is false
        /// </summary>
        [Fact]
        public void TargetFinishedNullBuildEventContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTargetFinished(null, "MyTarget", "ProjectFile", "ProjectFileOfTarget", true, null);
            }
           );
        }
        /// <summary>
        /// Test the case where TargetName is null.
        /// </summary>
        [Fact]
        public void TargetFinishedNullTargetName()
        {
            TestTargetFinished(null, "ProjectFile", "ProjectFileOfTarget", true);
            TestTargetFinished(String.Empty, "ProjectFile", "ProjectFileOfTarget", true);

            TestTargetFinished("TargetName", null, "ProjectFileOfTarget", true);
            TestTargetFinished("TargetName", String.Empty, "ProjectFileOfTarget", true);

            TestTargetFinished("TargetName", "ProjectFile", null, true);
            TestTargetFinished("TargetName", "ProjectFile", String.Empty, true);

            TestTargetFinished("TargetName", "ProjectFile", "ProjectFileOfTarget", true);
            TestTargetFinished("TargetName", "ProjectFile", "ProjectFileOfTarget", false);
        }
        #endregion

        #endregion

        #endregion

        #region LogTelemetry

        /// <summary>
        /// Verifies an InternalErrorException is thrown when a null event name is passed in
        /// </summary>
        [Fact]
        public void LogTelemetryNullEventName()
        {
            InternalErrorException exception = Assert.Throws<InternalErrorException>(() =>
            {
                ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
                service.LogTelemetry(
                    buildEventContext: null,
                    eventName: null,
                    properties: new Dictionary<string, string>());;
            });

            Assert.Contains("eventName is null", exception.Message);
        }

        [Fact]
        public void LogTelemetryTest()
        {
            IDictionary<string, string> eventProperties = new Dictionary<string, string>
            {
                {"Property1", "Value1"},
                {"Property2", "347EA055D5BD405F9726D7429BB30244"},
                {"Property3", @"C:\asdf\asdf\asdf"},
            };

            TestLogTelemetry(buildEventContext: null, eventName: "no context and no properties", properties: null);
            TestLogTelemetry(buildEventContext: null, eventName: "no context but with properties", properties: eventProperties);
            TestLogTelemetry(buildEventContext: s_buildEventContext, eventName: "event context but no properties", properties: null);
            TestLogTelemetry(buildEventContext: s_buildEventContext, eventName: "event context and properties", properties: eventProperties);
        }

        private void TestLogTelemetry(BuildEventContext buildEventContext, string eventName, IDictionary<string, string> properties)
        {
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            service.LogTelemetry(buildEventContext, eventName, properties);

            TelemetryEventArgs expectedEventArgs = new TelemetryEventArgs
            {
                EventName = eventName,
                BuildEventContext = buildEventContext,
                Properties = properties == null ? new Dictionary<string, string>() : new Dictionary<string, string>(properties),
            };


            TelemetryEventArgs actualEventArgs = (TelemetryEventArgs)service.ProcessedBuildEvent;

            Assert.Equal(expectedEventArgs.EventName, actualEventArgs.EventName);
            Assert.Equal(expectedEventArgs.Properties.OrderBy(kvp => kvp.Key, StringComparer.Ordinal), actualEventArgs.Properties.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(expectedEventArgs.BuildEventContext, actualEventArgs.BuildEventContext);

            if (properties != null)
            {
                // Ensure the properties were cloned into a new dictionary
                Assert.False(Object.ReferenceEquals(actualEventArgs.Properties, properties));
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Generate a message from an exception and a resource string. This is used for both errors and warnings.
        /// </summary>
        /// <param name="exception">Exception to add to end of message</param>
        /// <param name="resourceName">Resource name to generate message from</param>
        /// <param name="code">Error or Warning code which is output from FormatResourceString</param>
        /// <param name="helpKeyword">output HelpKeyword</param>
        /// <param name="message">output message</param>
        /// <param name="parameters">parameters to use in format resource string</param>
        private void GenerateMessageFromExceptionAndResource(Exception exception, string resourceName, out string code, out string helpKeyword, out string message, params string[] parameters)
        {
            message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out code, out helpKeyword, resourceName, parameters);
#if DEBUG
            message += Environment.NewLine + "This is an unhandled exception from a task -- PLEASE OPEN A BUG AGAINST THE TASK OWNER.";
#endif
            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }
        }

        /// <summary>
        /// Verify LogErrorFromText
        /// </summary>
        /// <param name="errorCode">ErrorCode to test</param>
        /// <param name="helpKeyword">HelpKeyword to test</param>
        /// <param name="subcategoryKey">SubCategory which will be used to get the Subcategory</param>
        /// <param name="message">Message to test</param>
        private void TestLogErrorFromText(string errorCode, string helpKeyword, string subcategoryKey, string message)
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string subcategory = null;
            if (subcategoryKey != null)
            {
                subcategory = AssemblyResources.GetString(subcategoryKey);
            }

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogErrorFromText(s_buildEventContext, subcategoryKey, errorCode, helpKeyword, fileInfo, message);
            VerifyBuildErrorEventArgs(fileInfo, errorCode, helpKeyword, message, service, subcategory);
        }

        /// <summary>
        /// Verify LogWarningFromText
        /// </summary>
        /// <param name="warningCode">WarningCode to test</param>
        /// <param name="helpKeyword">HelpKeyword to test</param>
        /// <param name="subcategoryKey">SubCategory which will be used to get the Subcategory</param>
        /// <param name="message">Message to test</param>
        private void TestLogWarningFromText(string warningCode, string helpKeyword, string subcategoryKey, string message)
        {
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string subcategory = null;
            if (subcategoryKey != null)
            {
                subcategory = AssemblyResources.GetString(subcategoryKey);
            }

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogWarningFromText(s_buildEventContext, subcategoryKey, warningCode, helpKeyword, fileInfo, message);
            VerifyBuildWarningEventArgs(fileInfo, warningCode, helpKeyword, message, service, subcategory);
        }

        /// <summary>
        /// Test LogWarning
        /// </summary>
        /// <param name="taskName">TaskName to test</param>
        /// <param name="subCategoryKey">SubCategoryKey to test</param>
        private void TestLogWarning(string taskName, string subCategoryKey)
        {
            string subcategory = AssemblyResources.GetString(subCategoryKey);
            BuildEventFileInfo fileInfo = new BuildEventFileInfo("foo.cs", 1, 2, 3, 4);
            string warningCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out warningCode, out helpKeyword, "FatalTaskError", taskName);
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);

            service.LogWarning(s_buildEventContext, subCategoryKey, fileInfo, "FatalTaskError", taskName);
            VerifyBuildWarningEventArgs(fileInfo, warningCode, helpKeyword, message, service, subcategory);
        }

        /// <summary>
        /// Test ProjectFinishedEvent
        /// </summary>
        /// <param name="projectFile">Project File to Test</param>
        /// <param name="success">Success value to test</param>
        private void TestProjectFinishedEvent(string projectFile, bool success)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword((success ? "ProjectFinishedSuccess" : "ProjectFinishedFailure"), Path.GetFileName(projectFile));
            MockHost componentHost = new MockHost();
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1, componentHost);
            try
            {
                service.LogProjectFinished(s_buildEventContext, projectFile, success);
            }
            catch (InternalErrorException ex)
            {
                Assert.Contains("ContextID " + s_buildEventContext.ProjectContextId, ex.Message);
            }
            finally
            {
                service.ResetProcessedBuildEvent();
            }

            ConfigCache cache = (ConfigCache)componentHost.GetComponent(BuildComponentType.ConfigCache);

            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(2, data, "4.0");
            cache.AddConfiguration(config);

            // Now do it the right way -- with a matching ProjectStarted.
            BuildEventContext projectContext = service.LogProjectStarted
                (
                    new BuildEventContext(1, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId),
                    1,
                    2,
                    s_buildEventContext,
                    projectFile,
                    null,
                    null,
                    null
                );

            service.LogProjectFinished(projectContext, projectFile, success);

            VerifyProjectFinishedEvent(service, projectContext, message, projectFile, success);

            service.ResetProcessedBuildEvent();
        }

        /// <summary>
        /// Test TaskStartedEvent
        /// </summary>
        /// <param name="taskName">TaskName to test</param>
        /// <param name="projectFile">ProjectFile to test</param>
        /// <param name="projectFileOfTask">ProjectFileOfTask to test</param>
        private void TestTaskStartedEvent(string taskName, string projectFile, string projectFileOfTask)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TaskStarted", taskName);

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogTaskStarted(s_buildEventContext, taskName, projectFile, projectFileOfTask);
            VerifyTaskStartedEvent(taskName, projectFile, projectFileOfTask, message, service);

            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogTaskStarted(s_buildEventContext, taskName, projectFile, projectFileOfTask);
            Assert.Null(service.ProcessedBuildEvent);
        }

        /// <summary>
        /// Test task Finished event
        /// </summary>
        /// <param name="taskName">TaskName to test</param>
        /// <param name="projectFile">ProjectFile to test</param>
        /// <param name="projectFileOfTask">ProjectFileOfTask to test</param>
        /// <param name="succeeded">Succeeded value to test</param>
        private void TestTaskFinished(string taskName, string projectFile, string projectFileOfTask, bool succeeded)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword((succeeded ? "TaskFinishedSuccess" : "TaskFinishedFailure"), taskName);
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogTaskFinished(s_buildEventContext, taskName, projectFile, projectFileOfTask, succeeded);
            VerifyTaskFinishedEvent(taskName, projectFile, projectFileOfTask, succeeded, message, service);

            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogTaskFinished(s_buildEventContext, taskName, projectFile, projectFileOfTask, succeeded);
            Assert.Null(service.ProcessedBuildEvent);
        }

        /// <summary>
        /// Test the TargetFinished event
        /// </summary>
        /// <param name="targetName">TargetName to test</param>
        /// <param name="projectFile">ProjectFile to test</param>
        /// <param name="projectFileOfTarget">ProjectFileOftarget to test</param>
        /// <param name="succeeded">Succeeded value to test</param>
        private void TestTargetFinished(string targetName, string projectFile, string projectFileOfTarget, bool succeeded)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword((succeeded ? "TargetFinishedSuccess" : "TargetFinishedFailure"), targetName, Path.GetFileName(projectFile));
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            List<TaskItem> outputs = new List<TaskItem>();
            outputs.Add(new TaskItem("ItemInclude", projectFile));
            service.LogTargetFinished(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, succeeded, outputs);
            VerifyTargetFinishedEvent(targetName, projectFile, projectFileOfTarget, succeeded, message, service, outputs);

            // Test OnlyLogCriticalEvents
            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogTargetFinished(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, succeeded, outputs);
            Assert.Null(service.ProcessedBuildEvent);
        }

        /// <summary>
        /// Test the targetStarted event
        /// </summary>
        /// <param name="targetName">TargetName to test</param>
        /// <param name="projectFile">Project file to test</param>
        /// <param name="projectFileOfTarget">ProjectFileOfTarget to test</param>
        private void TestTargetStartedEvent(string targetName, string projectFile, string projectFileOfTarget)
        {
            string message = String.Empty;

            if (String.Equals(projectFile, projectFileOfTarget, StringComparison.OrdinalIgnoreCase))
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TargetStartedProjectEntry", targetName, projectFile);
            }
            else
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TargetStartedFileProjectEntry", targetName, projectFileOfTarget, projectFile);
            }

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogTargetStarted(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, String.Empty, TargetBuiltReason.None);
            VerifyTargetStartedEvent(targetName, projectFile, projectFileOfTarget, message, service);

            // Do not expect to have any event logged when OnlyLogCriticalEvents is true
            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogTargetStarted(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, null, TargetBuiltReason.None);
            Assert.Null(service.ProcessedBuildEvent);
        }

        /// <summary>
        /// Test the targetStarted event when there is a parent target
        /// </summary>
        private void TestTargetStartedWithParentTargetEvent(string targetName, string projectFile, string projectFileOfTarget)
        {
            string parentTargetName = "MyParentTarget";
            string message = String.Empty;
            if (String.Equals(projectFile, projectFileOfTarget, StringComparison.OrdinalIgnoreCase))
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TargetStartedProjectDepends", targetName, projectFile, parentTargetName);
            }
            else
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("TargetStartedFileProjectDepends", targetName, projectFileOfTarget, projectFile, parentTargetName);
            }

            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1);
            service.LogTargetStarted(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, parentTargetName, TargetBuiltReason.AfterTargets);
            VerifyTargetStartedEvent(targetName, projectFile, projectFileOfTarget, message, service);

            // Do not expect to have any event logged when OnlyLogCriticalEvents is true
            service.ResetProcessedBuildEvent();
            service.OnlyLogCriticalEvents = true;
            service.LogTargetStarted(s_targetBuildEventContext, targetName, projectFile, projectFileOfTarget, parentTargetName, TargetBuiltReason.BeforeTargets);
            Assert.Null(service.ProcessedBuildEvent);
        }

        /// <summary>
        /// Test LogProjectStarted
        /// </summary>
        private void LogProjectStartedTestHelper(string projectFile, string targetNames)
        {
            string message = string.Empty;
            if (!String.IsNullOrEmpty(targetNames))
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ProjectStartedPrefixForTopLevelProjectWithTargetNames", Path.GetFileName(projectFile), targetNames);
            }
            else
            {
                message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ProjectStartedPrefixForTopLevelProjectWithDefaultTargets", Path.GetFileName(projectFile));
            }

            MockHost componentHost = new MockHost();
            ProcessBuildEventHelper service = (ProcessBuildEventHelper)ProcessBuildEventHelper.CreateLoggingService(LoggerMode.Synchronous, 1, componentHost);
            ConfigCache cache = (ConfigCache)componentHost.GetComponent(BuildComponentType.ConfigCache);

            BuildRequestData data = new BuildRequestData("file", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "toolsVersion", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(2, data, "4.0");
            cache.AddConfiguration(config);

            BuildEventContext context = service.LogProjectStarted(s_buildEventContext, 1, 2, s_buildEventContext, projectFile, targetNames, null, null);
            BuildEventContext parentBuildEventContext = s_buildEventContext;
            VerifyProjectStartedEventArgs(service, context.ProjectContextId, message, projectFile, targetNames, parentBuildEventContext, context);

            service.ResetProcessedBuildEvent();
        }

        /// <summary>
        /// Create a TargetFinished event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="targetName">TargetName to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="projectFileOfTarget">ProjectFileOfTarget to create the comparison event with.</param>
        /// <param name="succeeded">Succeeded value to create the comparison event with.</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        private void VerifyTargetFinishedEvent(string targetName, string projectFile, string projectFileOfTarget, bool succeeded, string message, ProcessBuildEventHelper service, IEnumerable targetOutputs)
        {
            TargetFinishedEventArgs targetEvent = new TargetFinishedEventArgs
                (
                  message,
                  null,
                  targetName,
                  projectFile,
                  projectFileOfTarget,
                  succeeded,
                  service.ProcessedBuildEvent.Timestamp,
                  targetOutputs
                );
            targetEvent.BuildEventContext = s_targetBuildEventContext;
            Assert.True(((TargetFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(targetEvent));
        }

        /// <summary>
        /// Create a TargetStarted event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="targetName">TaskName to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="projectFileOfTarget">ProjectFileOfTarget to create the comparison event with.</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        private void VerifyTargetStartedEvent(string targetName, string projectFile, string projectFileOfTarget, string message, ProcessBuildEventHelper service)
        {
            TargetStartedEventArgs buildEvent = new TargetStartedEventArgs
                       (
                           message,
                           null, // no help keyword
                           targetName,
                           projectFile,
                           projectFileOfTarget,
                           String.Empty,
                           TargetBuiltReason.None,
                           service.ProcessedBuildEvent.Timestamp
                       );
            buildEvent.BuildEventContext = s_targetBuildEventContext;
            Assert.True(((TargetStartedEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildEvent));
        }

        /// <summary>
        /// Create a TaskFinished event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="taskName">TaskName to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="projectFileOfTask">ProjectFileOfTask to create the comparison event with.</param>
        /// <param name="succeeded">Succeeded value to create the comparison event with.</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        private void VerifyTaskFinishedEvent(string taskName, string projectFile, string projectFileOfTask, bool succeeded, string message, ProcessBuildEventHelper service)
        {
            TaskFinishedEventArgs taskEvent = new TaskFinishedEventArgs
                (
                  message,
                  null,
                  projectFile,
                  projectFileOfTask,
                  taskName,
                  succeeded,
                  service.ProcessedBuildEvent.Timestamp
                );
            taskEvent.BuildEventContext = s_buildEventContext;
            Assert.True(((TaskFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(taskEvent));
        }

        /// <summary>
        /// Create a taskStarted event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="taskName">TaskName to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="projectFileOfTask">ProjectFileOfTask to create the comparison event with.</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        private void VerifyTaskStartedEvent(string taskName, string projectFile, string projectFileOfTask, string message, ProcessBuildEventHelper service)
        {
            TaskStartedEventArgs taskEvent = new TaskStartedEventArgs
                (
                 message,
                  null, // no help keyword
                  projectFile,
                  projectFileOfTask,
                  taskName,
                  service.ProcessedBuildEvent.Timestamp
                );
            taskEvent.BuildEventContext = s_buildEventContext;
            Assert.True(((TaskStartedEventArgs)service.ProcessedBuildEvent).IsEquivalent(taskEvent));
        }

        /// <summary>
        /// Create a projectFinished event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        /// <param name="projectContext">The build event context that this ProjectFinished event should contain</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="success">Success value to create the comparison event with</param>
        private void VerifyProjectFinishedEvent(ProcessBuildEventHelper service, BuildEventContext projectContext, string message, string projectFile, bool success)
        {
            ProjectFinishedEventArgs projectEvent = new ProjectFinishedEventArgs
                (
                  message,
                  null,
                  projectFile,
                  success,
                  service.ProcessedBuildEvent.Timestamp
                );
            projectEvent.BuildEventContext = projectContext;
            Assert.True(((ProjectFinishedEventArgs)service.ProcessedBuildEvent).IsEquivalent(projectEvent));
        }

        /// <summary>
        /// Create a projectStarted event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        /// <param name="projectId">ProjectId to create the comparison event with.</param>
        /// <param name="message">Message to create the comparison event with.</param>
        /// <param name="projectFile">ProjectFile to create the comparison event with.</param>
        /// <param name="targetNames">TargetNames to create the comparison event with.</param>
        /// <param name="parentBuildEventContext">ParentBuildEventContext to create the comparison event with.</param>
        private void VerifyProjectStartedEventArgs(ProcessBuildEventHelper service, int projectId, string message, string projectFile, string targetNames, BuildEventContext parentBuildEventContext, BuildEventContext generatedContext)
        {
            ProjectStartedEventArgs buildEvent = new ProjectStartedEventArgs
                    (
                        projectId,
                        message,
                        null,       // no help keyword
                        projectFile,
                        targetNames,
                        null,
                        null,
                      parentBuildEventContext,
                        service.ProcessedBuildEvent.Timestamp
                    );
            buildEvent.BuildEventContext = generatedContext;
            Assert.True(((ProjectStartedEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildEvent));
        }

        /// <summary>
        /// Create a buildMessage event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        /// <param name="messageImportance">Importance level create the comparison event with</param>
        /// <param name="message">Message to create the comparison event with</param>
        private void VerityBuildMessageEventArgs(ProcessBuildEventHelper service, MessageImportance messageImportance, string message)
        {
            BuildMessageEventArgs buildMessageEvent = new BuildMessageEventArgs
                (
                  message,
                  null,
                  "MSBuild",
                  messageImportance,
                  service.ProcessedBuildEvent.Timestamp
                );

            buildMessageEvent.BuildEventContext = s_buildEventContext;
            Assert.True(((BuildMessageEventArgs)service.ProcessedBuildEvent).IsEquivalent(buildMessageEvent));
        }

        /// <summary>
        /// Create a buildWarning event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="fileInfo">FileInfo to create the comparison event with</param>
        /// <param name="warningCode">Warningcode to create the comparison event with c</param>
        /// <param name="helpKeyword">helpKeyword to create the comparison event with</param>
        /// <param name="message">message to create the comparison event with</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        /// <param name="subcategory">Subcategory to create the comparison event with</param>
        private void VerifyBuildWarningEventArgs(BuildEventFileInfo fileInfo, string warningCode, string helpKeyword, string message, ProcessBuildEventHelper service, string subcategory)
        {
            BuildWarningEventArgs buildEvent = new BuildWarningEventArgs
                (
                    subcategory,
                    warningCode,
                    fileInfo.File,
                    fileInfo.Line,
                    fileInfo.Column,
                    fileInfo.EndLine,
                    fileInfo.EndColumn,
                    message,
                    helpKeyword,
                    "MSBuild",
                    service.ProcessedBuildEvent.Timestamp
                );
            buildEvent.BuildEventContext = s_buildEventContext;
            Assert.True(buildEvent.IsEquivalent((BuildWarningEventArgs)service.ProcessedBuildEvent));
        }

        /// <summary>
        /// Create a buildError event to compare to the one which was passed into the ProcessedBuildEvent method.
        /// </summary>
        /// <param name="fileInfo">FileInfo to create the comparison event with</param>
        /// <param name="errorCode">Errorcode to create the comparison event with c</param>
        /// <param name="helpKeyword">helpKeyword to create the comparison event with</param>
        /// <param name="message">message to create the comparison event with</param>
        /// <param name="service">LoggingService mock object which overrides ProcessBuildEvent and can provide a ProcessedBuildEvent (the event which would have been sent to the loggers)</param>
        /// <param name="subcategory">Subcategory to create the comparison event with</param>
        private void VerifyBuildErrorEventArgs(BuildEventFileInfo fileInfo, string errorCode, string helpKeyword, string message, ProcessBuildEventHelper service, string subcategory)
        {
            BuildErrorEventArgs buildEvent = new BuildErrorEventArgs
                (
                    subcategory,
                    errorCode,
                    fileInfo.File,
                    fileInfo.Line,
                    fileInfo.Column,
                    fileInfo.EndLine,
                    fileInfo.EndColumn,
                    message,
                    helpKeyword,
                    "MSBuild",
                    service.ProcessedBuildEvent.Timestamp
                );
            buildEvent.BuildEventContext = s_buildEventContext;
            Assert.True(buildEvent.IsEquivalent((BuildErrorEventArgs)service.ProcessedBuildEvent));
        }

        /// <summary>
        /// Log a given build event and verify it is sent to ProcessLoggingEvent
        /// </summary>
        /// <param name="expectedBuildEvent">BuildEvent to log and expect from ProcessLoggingEvent</param>
        /// <param name="loggingService">LoggingService to log event to</param>
        private void LogandVerifyBuildEvent(BuildEventArgs expectedBuildEvent, ProcessBuildEventHelper loggingService)
        {
            loggingService.LogBuildEvent(expectedBuildEvent);
            Assert.True(loggingService.ProcessedBuildEvent.IsEquivalent(expectedBuildEvent)); // "Expected ProcessedBuildEvent to equal expected build event"
            loggingService.ResetProcessedBuildEvent();
        }
        #endregion

        #region Helper Classes
        /// <summary>
        /// Create a derived class which overrides ProcessLoggingEvent so
        /// we can test most of the logging methods without relying on the
        /// exact implementation of process logging events.
        /// </summary>
        internal class ProcessBuildEventHelper : LoggingService
        {
            #region Data
            /// <summary>
            /// Event processed by ProcessLoggingEvent. This can be asserted in a test
            /// to verify that a buildEvent was sent to ProcessLoggingEvent.
            /// </summary>
            private BuildEventArgs _processedBuildEvent;
            #endregion
            #region Constructor
            /// <summary>
            /// Create a constructor which calls the base class constructor
            /// </summary>
            /// <param name="loggerMode">Is the logging service supposed to be Synchronous or Asynchronous</param>
            protected ProcessBuildEventHelper(LoggerMode loggerMode, int nodeId, IBuildComponentHost componentHost)
                : base(loggerMode, nodeId)
            {
                if (componentHost == null)
                {
                    componentHost = new MockHost();
                }

                InitializeComponent(componentHost);
            }
            #endregion

            #region Properties
            /// <summary>
            /// Accessor for the event processed by ProcessLoggingEvent
            /// </summary>
            public BuildEventArgs ProcessedBuildEvent
            {
                get
                {
                    return _processedBuildEvent;
                }
            }
            #endregion

            #region Methods

            /// <summary>
            /// Create a new instance of a LoggingServiceOverrideProcessBuildEvent class
            /// </summary>
            /// <param name="mode">Logger mode, this is not used</param>
            /// <returns>Instantiated LoggingServiceOverrideProcessBuildEvent</returns>
            public new static IBuildComponent CreateLoggingService(LoggerMode mode, int nodeId)
            {
                return new ProcessBuildEventHelper(mode, nodeId, null);
            }

            /// <summary>
            /// Create a new instance of a LoggingServiceOverrideProcessBuildEvent class
            /// </summary>
            /// <param name="mode">Logger mode, this is not used</param>
            /// <returns>Instantiated LoggingServiceOverrideProcessBuildEvent</returns>
            public static IBuildComponent CreateLoggingService(LoggerMode mode, int nodeId, IBuildComponentHost componentHost)
            {
                return new ProcessBuildEventHelper(mode, nodeId, componentHost);
            }

            /// <summary>
            /// Override the method to log which event was processed so it can be verified in a test
            /// </summary>
            /// <param name="buildEvent">Build event which was asked to be processed</param>
            internal override void ProcessLoggingEvent(object buildEvent, bool allowThrottling = false)
            {
                if (buildEvent is BuildEventArgs)
                {
                    _processedBuildEvent = buildEvent as BuildEventArgs;
                }
                else if (buildEvent is KeyValuePair<int, BuildEventArgs>)
                {
                    _processedBuildEvent = ((KeyValuePair<int, BuildEventArgs>)buildEvent).Value;
                }
                else
                {
                    _processedBuildEvent = null;
                }
            }

            /// <summary>
            /// Reset the event processed by ProcessLoggingEvent.
            /// This is done so another event can be logged.
            /// </summary>
            internal void ResetProcessedBuildEvent()
            {
                _processedBuildEvent = null;
            }
            #endregion
        }

        private class EventArgsEqualityComparer<T> : IEqualityComparer<T> where T : BuildEventArgs
        {
            public bool Equals(T x, T y)
            {
                return x.IsEquivalent(y);
            }

            public int GetHashCode(T obj)
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }
}
