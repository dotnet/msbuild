// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.ProjectCache
{
    /// <summary>
    ///     Events logged with this logger will get pushed into MSBuild's logging infrastructure.
    /// </summary>
    public abstract class PluginLoggerBase
    {
        public abstract bool HasLoggedErrors { get; protected set; }

        public abstract void LogMessage(string message, MessageImportance? messageImportance = null);

        public abstract void LogWarning(string warning);

        public abstract void LogError(string error);
    }
}
