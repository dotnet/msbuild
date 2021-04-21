// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Cli
{
    internal class ExtendedTemplateEngineHost : ITemplateEngineHost
    {
        private readonly New3Command _new3Command;
        private readonly ITemplateEngineHost _baseHost;

        internal ExtendedTemplateEngineHost(ITemplateEngineHost baseHost, New3Command new3Command)
        {
            _baseHost = baseHost;
            _new3Command = new3Command;
        }

        public IPhysicalFileSystem FileSystem => _baseHost.FileSystem;

        public string HostIdentifier => _baseHost.HostIdentifier;

        public IReadOnlyList<string> FallbackHostTemplateConfigNames => _baseHost.FallbackHostTemplateConfigNames;

        public string Version => _baseHost.Version;

        public virtual IReadOnlyList<KeyValuePair<Guid, Func<Type>>> BuiltInComponents => _baseHost.BuiltInComponents;

        private bool GlobalJsonFileExistsInPath
        {
            get
            {
                const string fileName = "global.json";

                string workingPath = Path.Combine(FileSystem.GetCurrentDirectory(), _new3Command.OutputPath);
                bool found = false;

                do
                {
                    string checkPath = Path.Combine(workingPath, fileName);
                    found = FileSystem.FileExists(checkPath);
                    if (!found)
                    {
                        workingPath = Path.GetDirectoryName(workingPath.TrimEnd('/', '\\'));

                        if (!FileSystem.DirectoryExists(workingPath))
                        {
                            workingPath = null;
                        }
                    }
                }
                while (!found && (workingPath != null));

                return found;
            }
        }

        public virtual void LogMessage(string message)
        {
            Reporter.Output.WriteLine(message);
        }

        public virtual void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericError, message));
        }

        public virtual bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericWarning, message));
            return false;
        }

        public virtual bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            return _baseHost.OnParameterError(parameter, receivedValue, message, out newValue);
        }

        public virtual void OnSymbolUsed(string symbol, object value) => _baseHost.OnSymbolUsed(symbol, value);

        public virtual bool TryGetHostParamDefault(string paramName, out string value)
        {
            switch (paramName)
            {
                case "GlobalJsonExists":
                    value = GlobalJsonFileExistsInPath.ToString();
                    return true;
                default:
                    return _baseHost.TryGetHostParamDefault(paramName, out value);
            }
        }

        public void VirtualizeDirectory(string path)
        {
            _baseHost.VirtualizeDirectory(path);
        }

        public bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            Reporter.Error.WriteLine(LocalizableStrings.DestructiveChangesNotification.Bold().Red());
            int longestChangeTextLength = destructiveChanges.Max(x => GetChangeString(x.ChangeKind).Length);
            int padLen = 5 + longestChangeTextLength;

            foreach (IFileChange change in destructiveChanges)
            {
                string changeKind = GetChangeString(change.ChangeKind);
                Reporter.Error.WriteLine(($"  {changeKind}".PadRight(padLen) + change.TargetRelativePath).Bold().Red());
            }

            Reporter.Error.WriteLine();
            Reporter.Error.WriteLine(LocalizableStrings.RerunCommandAndPassForceToCreateAnyway.Bold().Red());
            return false;
        }

        public bool OnConfirmPartialMatch(string name)
        {
            return true;
        }

        public void LogDiagnosticMessage(string message, string category, params string[] details)
        {
            _baseHost.LogDiagnosticMessage(message, category, details);
        }

        public virtual void LogTiming(string label, TimeSpan duration, int depth)
        {
            _baseHost.LogTiming(label, duration, depth);
        }

        private static string GetChangeString(ChangeKind kind)
        {
            string changeType;

            switch (kind)
            {
                case ChangeKind.Change:
                    changeType = LocalizableStrings.Change;
                    break;
                case ChangeKind.Delete:
                    changeType = LocalizableStrings.Delete;
                    break;
                case ChangeKind.Overwrite:
                    changeType = LocalizableStrings.Overwrite;
                    break;
                default:
                    changeType = LocalizableStrings.UnknownChangeKind;
                    break;
            }

            return changeType;
        }
    }
}
