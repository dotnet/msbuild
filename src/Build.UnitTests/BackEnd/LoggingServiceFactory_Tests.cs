// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    /// Test the Factory to create components of the type LoggingService
    /// </summary>
    public class LoggingServiceFactory_Tests
    {
        /// <summary>
        /// Verify we can create a synchronous LoggingService
        /// </summary>
        [Fact]
        public void TestCreateSynchronousLogger()
        {
            LoggingServiceFactory factory = new LoggingServiceFactory(LoggerMode.Synchronous, 1);
            LoggingService loggingService = (LoggingService)factory.CreateInstance(BuildComponentType.LoggingService);
            Assert.Equal(LoggerMode.Synchronous, loggingService.LoggingMode); // "Expected to create a Synchronous LoggingService"
        }

        /// <summary>
        /// Verify we can create a Asynchronous LoggingService
        /// </summary>
        [Fact]
        public void TestCreateAsynchronousLogger()
        {
            LoggingServiceFactory factory = new LoggingServiceFactory(LoggerMode.Asynchronous, 1);
            LoggingService loggingService = (LoggingService)factory.CreateInstance(BuildComponentType.LoggingService);
            Assert.Equal(LoggerMode.Asynchronous, loggingService.LoggingMode); // "Expected to create an Asynchronous LoggingService"
            loggingService.ShutdownComponent();
        }
    }
}
