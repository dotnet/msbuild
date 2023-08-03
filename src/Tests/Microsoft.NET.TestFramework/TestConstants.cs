// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class TestConstants
    {
        //  Intended to be used as an argument to XUnit's CollectionAttribute to prevent tests which change static telemetry state from interfering with each other
        public const string UsesStaticTelemetryState = nameof(UsesStaticTelemetryState);
    }
}
