// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PerfTraceEvent
    {
        public string Type { get; }
        public string Instance { get; }
        public DateTime StartUtc { get; }
        public TimeSpan Duration { get; }
        public IList<PerfTraceEvent> Children { get; }

        public PerfTraceEvent(string type, string instance, IEnumerable<PerfTraceEvent> children, DateTime startUtc, TimeSpan duration)
        {
            Type = type;
            Instance = instance;
            StartUtc = startUtc;
            Duration = duration;
            Children = children.OrderBy(e => e.StartUtc).ToList();
        }
    }
}
