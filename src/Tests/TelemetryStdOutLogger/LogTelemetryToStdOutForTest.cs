// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
