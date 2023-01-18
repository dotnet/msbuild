// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiSymbolExtensions.Logging
{
    /// <summary>
    /// Class to define common logging abstraction for MSBuild tasks across the APICompat and GenAPI codebases.
    /// </summary>
    public class MSBuildLog : ILog
    {
        internal readonly Logger _log;

        internal MSBuildLog(Logger log)
        {
            _log = log;
        }

        /// <inheritdoc />
        public void LogError(string code, string format, params string[] args) => _log.Log(new Message((NET.Build.Tasks.MessageLevel)MessageLevel.Error, string.Format(format, args), code));

        /// <inheritdoc />
        public void LogWarning(string code, string format, params string[] args) => _log.Log(new Message((NET.Build.Tasks.MessageLevel)MessageLevel.Warning, string.Format(format, args), code));

        /// <inheritdoc />
        public void LogMessage(MessageImportance importance, string format, params string[] args) => _log.LogMessage((Build.Framework.MessageImportance)importance, format, args);
    }
}
