// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Engine.UnitTests
{
    internal sealed class TestLoggingContext : LoggingContext
    {
        public TestLoggingContext(ILoggingService? loggingService, BuildEventContext eventContext) : base(
            loggingService ?? Build.BackEnd.Logging.LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1), eventContext)
        {
            IsValid = true;
        }

        public static LoggingContext CreateTestContext(BuildEventContext buildEventContext)
        {
            return new TestLoggingContext(null, buildEventContext);
        }
    }
}
