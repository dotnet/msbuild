// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Logging context and helpers for evaluation logging
    /// </summary>
    internal class MockLoggingContext : LoggingContext
    {
        public MockLoggingContext(ILoggingService loggingService, BuildEventContext eventContext) : base(loggingService, eventContext)
        {
            IsValid = true;
        }
    }
}
