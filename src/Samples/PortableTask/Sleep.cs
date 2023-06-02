// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

#nullable disable

namespace PortableTask
{
    public class Sleep : Microsoft.Build.Utilities.Task
    {
        public double Seconds { get; set; }

        public override bool Execute()
        {
            Task.Delay(TimeSpan.FromSeconds(Seconds)).Wait();
            return true;
        }
    }
}
