// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.CommandLine.UnitTests.TestAssets.TargetInvocationException.LoggerProject
{
    public class FaultyLogger : ILogger
    {
        public FaultyLogger()
        {
            throw new Exception("Constructor failed intentionally.");
        }

        public string Parameters { get; set; }
        public LoggerVerbosity Verbosity { get; set; }

        public void Initialize(IEventSource eventSource) { }

        public void Shutdown() { }
    }
}
