// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO;

namespace ThreadSafeTask
{
    public class ThreadSafeTask : Microsoft.Build.Utilities.Task, Microsoft.Build.Framework.IThreadSafeTask
    {
        public override bool Execute()
        {
            Log.LogMessage($"Message from threadsafe task");

            // danger: analyzer should flag this
            File.WriteAllText("hello.txt", "Hello, world!");

            return true;
        }
    }
}
