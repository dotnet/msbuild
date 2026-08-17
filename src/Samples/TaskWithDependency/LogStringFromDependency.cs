// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dependency;

#nullable disable

namespace TaskWithDependency
{
    public class LogStringFromDependency : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            Log.LogMessage($"Message from dependency: {Alpha.GetString()}");

            return true;
        }
    }
}
