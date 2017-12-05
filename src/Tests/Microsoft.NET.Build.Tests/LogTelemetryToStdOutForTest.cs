// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.NET.Build.Tests
{
    public sealed class LogTelemetryToStdOutForTest : Logger
    {

        public LogTelemetryToStdOutForTest()
        {
        }

        public override void Initialize(IEventSource eventSource)
        {
            if (eventSource is IEventSource2 eventSource2)
            {
                eventSource2.TelemetryLogged += OnTelemetryLogged;
            }
        }

        private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
        {
            Console.WriteLine(JsonConvert.SerializeObject(args));
        }
    }
}
