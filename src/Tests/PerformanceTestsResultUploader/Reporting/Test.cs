// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Reporting
{
    public class Test
    {
        public IList<string> Categories { get; set; } = new List<string>();

        public string Name { get; set; }

        public IList<Counter> Counters { get; set; } = new List<Counter>();

        public void AddCounter(Counter counter)
        {
            if (counter.DefaultCounter && Counters.Any(c => c.DefaultCounter))
                throw new Exception($"Duplicate default counter, name: ${counter.Name}");
            Counters.Add(counter);
        }
    }
}
