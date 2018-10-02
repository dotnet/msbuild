// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd;
using Xunit;

namespace Microsoft.Build.UnitTests.Logging
{
    /// <summary>
    ///Test the Factory to create components of the type LoggingService
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
            Assert.Equal(loggingService.LoggingMode, LoggerMode.Synchronous); // "Expected to create a Synchronous LoggingService"
        }

        /// <summary>
        /// Verify we can create a Asynchronous LoggingService
        /// </summary>
        [Fact]
        public void TestCreateAsynchronousLogger()
        {
            LoggingServiceFactory factory = new LoggingServiceFactory(LoggerMode.Asynchronous, 1);
            LoggingService loggingService = (LoggingService)factory.CreateInstance(BuildComponentType.LoggingService);
            Assert.Equal(loggingService.LoggingMode, LoggerMode.Asynchronous); // "Expected to create an Asynchronous LoggingService"
        }
    }
}