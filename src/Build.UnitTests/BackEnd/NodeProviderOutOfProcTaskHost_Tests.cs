// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for NodeProviderOutOfProcTaskHost, specifically error handling for unsupported runtime options.
    /// </summary>
    public class NodeProviderOutOfProcTaskHost_Tests : IDisposable
    {
        private IBuildComponent _nodeProviderComponent;
        private NodeProviderOutOfProcTaskHost _nodeProvider;
        private MockHost _mockHost;

        public NodeProviderOutOfProcTaskHost_Tests()
        {
            _mockHost = new MockHost();
            _nodeProviderComponent = NodeProviderOutOfProcTaskHost.CreateComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
            _nodeProvider = _nodeProviderComponent as NodeProviderOutOfProcTaskHost;
            _nodeProvider.InitializeComponent(_mockHost);
        }

        public void Dispose()
        {
            _nodeProvider?.ShutdownComponent();
            _mockHost = null;
            _nodeProvider = null;
            _nodeProviderComponent = null;
        }

        /// <summary>
        /// Verify that attempting to acquire a task host with the .NET runtime flag throws InvalidProjectFileException
        /// with the MSB4233 error code and includes the task name and assembly location.
        /// This should only happen on .NET Framework builds of MSBuild.
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void AcquireAndSetUpHost_WithNETRuntime_ThrowsInvalidProjectFileException()
        {
            // This test uses HandshakeOptions which is internal to Microsoft.Build and only accessible
            // from test projects on NETFRAMEWORK builds due to InternalsVisibleTo. The WindowsFullFrameworkOnlyFact
            // attribute skips test execution on non-Framework builds, but we also need the #if to skip compilation.
#if NETFRAMEWORK
            // Arrange
            HandshakeOptions hostContext = HandshakeOptions.TaskHost | HandshakeOptions.NET | HandshakeOptions.X64;
            string taskName = "MyNetTask";
            string taskLocation = "C:\\Path\\To\\MyTask.dll";
            string projectFile = "C:\\Project\\test.proj";
            int lineNumber = 42;
            int columnNumber = 10;

            TaskHostConfiguration configuration = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Environment.CurrentDirectory,
                buildProcessEnvironment: new Dictionary<string, string>(),
                culture: CultureInfo.CurrentCulture,
                uiCulture: CultureInfo.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: lineNumber,
                columnNumberOfTask: columnNumber,
                projectFileOfTask: projectFile,
                continueOnError: false,
                taskName: taskName,
                taskLocation: taskLocation,
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: new Dictionary<string, string>(),
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            // Act & Assert
            InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                _nodeProvider.AcquireAndSetUpHost(hostContext, null, null, configuration);
            });

            // Verify the exception contains the expected information (error code, task name, and task location are the important parts)
            exception.ErrorCode.ShouldBe("MSB4233");
            exception.Message.ShouldContain(taskName);
            exception.Message.ShouldContain(taskLocation);
#endif
        }

        /// <summary>
        /// Verify that acquiring a task host with CLR4 runtime (without NET flag) does not throw the MSB4233 error.
        /// Note: This test may fail if the task host cannot actually be launched, but that's expected
        /// in this environment. The important part is that it doesn't throw the MSB4233 error.
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void AcquireAndSetUpHost_WithoutNETRuntime_DoesNotThrowMSB4233()
        {
            // This test uses HandshakeOptions which is internal to Microsoft.Build and only accessible
            // from test projects on NETFRAMEWORK builds due to InternalsVisibleTo. The WindowsFullFrameworkOnlyFact
            // attribute skips test execution on non-Framework builds, but we also need the #if to skip compilation.
#if NETFRAMEWORK
            // Arrange
            HandshakeOptions hostContext = HandshakeOptions.TaskHost | HandshakeOptions.X64; // CLR4, no NET flag
            string taskName = "MyClr4Task";
            string taskLocation = "C:\\Path\\To\\MyTask.dll";
            string projectFile = "C:\\Project\\test.proj";

            TaskHostConfiguration configuration = new TaskHostConfiguration(
                nodeId: 1,
                startupDirectory: Environment.CurrentDirectory,
                buildProcessEnvironment: new Dictionary<string, string>(),
                culture: CultureInfo.CurrentCulture,
                uiCulture: CultureInfo.CurrentUICulture,
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                lineNumberOfTask: 1,
                columnNumberOfTask: 1,
                projectFileOfTask: projectFile,
                continueOnError: false,
                taskName: taskName,
                taskLocation: taskLocation,
                isTaskInputLoggingEnabled: false,
                taskParameters: null,
                globalParameters: new Dictionary<string, string>(),
                warningsAsErrors: null,
                warningsNotAsErrors: null,
                warningsAsMessages: null);

            // Act & Assert
            // We expect this to either succeed or fail with a different error (like node launch failure),
            // but NOT with MSB4233
            try
            {
                _nodeProvider.AcquireAndSetUpHost(hostContext, null, null, configuration);
                // If it succeeds (unlikely in test environment), that's fine
            }
            catch (InvalidProjectFileException ex)
            {
                // If it throws InvalidProjectFileException, it should NOT be MSB4233
                ex.ErrorCode.ShouldNotBe("MSB4233", $"Expected error other than MSB4233, but got: {ex.Message}");
            }
            catch (Exception)
            {
                // Other exceptions are fine for this test - we're only checking that MSB4233 isn't thrown
            }
#endif
        }
    }
}
