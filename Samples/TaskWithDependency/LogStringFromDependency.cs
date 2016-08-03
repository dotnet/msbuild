// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System.Reflection;
using Dependency;

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
