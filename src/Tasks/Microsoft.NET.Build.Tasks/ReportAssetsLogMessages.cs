// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.ProjectModel;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Report Log Messages in the assets file to MSBuild and raise them as
    /// DiagnosticMessage items that can be consumed downstream (e.g. by the
    /// dependency node in the solution explorer)
    /// </summary>
    public sealed class ReportAssetsLogMessages : TaskBase
    {
        private LockFile _lockFile;
        private DiagnosticsHelper _diagnostics;

        #region Outputs

        [Output]
        public ITaskItem[] DiagnosticMessages => _diagnostics.GetDiagnosticMessages();

        #endregion

        #region Inputs

        /// <summary>
        /// The assets file to process
        /// </summary>
        [Required]
        public string ProjectAssetsFile
        {
            get; set;
        }

        #endregion

        public ReportAssetsLogMessages()
        {
           _diagnostics = new DiagnosticsHelper(new MSBuildLog(Log));
        }

        #region Test Support

        internal ReportAssetsLogMessages(LockFile lockFile, ILog logger)
        {
            _lockFile = lockFile;
            _diagnostics = new DiagnosticsHelper(logger);
        }

        #endregion

        private LockFile LockFile
        {
            get
            {
                if (_lockFile == null)
                {
                    _lockFile = new LockFileCache(BuildEngine4).GetLockFile(ProjectAssetsFile);
                }

                return _lockFile;
            }
        }

        protected override void ExecuteCore()
        {
            foreach (var message in LockFile.LogMessages)
            {
                AddMessage(message);
            }
        }

        private void AddMessage(IAssetsLogMessage message)
        {
            var logToMsBuild = true;
            var targetGraphs = message.GetTargetGraphs(LockFile);

            targetGraphs = targetGraphs.Any() ? targetGraphs : new LockFileTarget[] { null };

            foreach (var target in targetGraphs)
            {
                var targetLib = message.LibraryId == null ? null : target?.GetTargetLibrary(message.LibraryId);

                _diagnostics.Add(
                    message.Code.ToString(),
                    message.Message,
                    message.FilePath,
                    FromLogLevel(message.Level),
                    message.StartLineNumber,
                    message.StartColumnNumber,
                    message.EndLineNumber,
                    message.EndColumnNumber,
                    target?.Name,
                    targetLib == null ? null : $"{targetLib.Name}/{targetLib.Version.ToNormalizedString()}",
                    logToMsBuild);

                logToMsBuild = false; // only write first instance of this diagnostic to msbuild
            }
        }

        private static DiagnosticMessageSeverity FromLogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return DiagnosticMessageSeverity.Error;

                case LogLevel.Warning:
                    return DiagnosticMessageSeverity.Warning;

                case LogLevel.Debug:
                case LogLevel.Verbose:
                case LogLevel.Information:
                case LogLevel.Minimal:
                default:
                    return DiagnosticMessageSeverity.Info;
            }
        }
    }
}
