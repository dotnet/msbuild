// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;

#nullable disable

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the Factory to create components of the type LoggingService
    /// </summary>
    [TestClass]
    public class LoggingServiceFactory_Tests
    {
        /// <summary>
        /// Verify we can create a synchronous LoggingService
        /// </summary>
        [MSBuildTestMethod]
        public void TestCreateSynchronousLogger()
        {
            LoggingServiceFactory factory = new LoggingServiceFactory(LoggerMode.Synchronous, 1);
            LoggingService loggingService = (LoggingService)factory.CreateInstance(BuildComponentType.LoggingService);
            Assert.AreEqual(LoggerMode.Synchronous, loggingService.LoggingMode); // "Expected to create a Synchronous LoggingService"
        }

        /// <summary>
        /// Verify we can create a Asynchronous LoggingService
        /// </summary>
        [MSBuildTestMethod]
        public void TestCreateAsynchronousLogger()
        {
            LoggingServiceFactory factory = new LoggingServiceFactory(LoggerMode.Asynchronous, 1);
            LoggingService loggingService = (LoggingService)factory.CreateInstance(BuildComponentType.LoggingService);
            Assert.AreEqual(LoggerMode.Asynchronous, loggingService.LoggingMode); // "Expected to create an Asynchronous LoggingService"
            loggingService.ShutdownComponent();
        }
    }
}
