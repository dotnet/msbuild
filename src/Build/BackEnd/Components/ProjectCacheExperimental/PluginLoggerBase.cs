// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.ProjectCache
{
#pragma warning disable CS0618  // suppress “obsolete” warnings in this file due to this referencing other Experimental.ProjectCache types
    /// <summary>
    ///     Events logged with this logger will get pushed into MSBuild's logging infrastructure.
    /// </summary>
    [Obsolete("This class is moved to Microsoft.Build.ProjectCache namespace.", false)]
    public abstract class PluginLoggerBase
    {
        public abstract bool HasLoggedErrors { get; protected set; }

        public abstract void LogMessage(string message, MessageImportance? messageImportance = null);

        public abstract void LogWarning(string warning);

        public abstract void LogError(string error);
    }
}
