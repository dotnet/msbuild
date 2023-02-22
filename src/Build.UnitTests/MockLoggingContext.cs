// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Logging context and helpers for evaluation logging
    /// </summary>
    internal sealed class MockLoggingContext : LoggingContext
    {
        public MockLoggingContext(ILoggingService loggingService, BuildEventContext eventContext) : base(loggingService, eventContext)
        {
            IsValid = true;
        }
    }
}
