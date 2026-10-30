// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Experimental.FileAccess
{
    /// <summary>
    /// Process data.
    /// </summary>
    [CLSCompliant(false)]
    public struct ProcessData : ITranslatable
    {
        private string _processName;
        private uint _processId;
        private uint _parentProcessId;
        private DateTime _creationDateTime;
        private DateTime _exitDateTime;
        private uint _exitCode;

        public ProcessData(string processName, uint processId, uint parentProcessId, DateTime creationDateTime, DateTime exitDateTime, uint exitCode)
        {
            _processName = processName;
            _processId = processId;
            _parentProcessId = parentProcessId;
            _creationDateTime = creationDateTime;
            _exitDateTime = exitDateTime;
            _exitCode = exitCode;
        }

        /// <summary>The process name.</summary>
        public string ProcessName
        {
            get => _processName;
            private set => _processName = value;
        }

        /// <summary>The process id.</summary>
        public uint ProcessId
        {
            get => _processId;
            private set => _processId = value;
        }

        /// <summary>The parent process id.</summary>
        public uint ParentProcessId
        {
            get => _parentProcessId;
            private set => _parentProcessId = value;
        }

        /// <summary>The creation date time.</summary>
        public DateTime CreationDateTime
        {
            get => _creationDateTime;
            private set => _creationDateTime = value;
        }

        /// <summary>The exit date time.</summary>
        public DateTime ExitDateTime
        {
            get => _exitDateTime;
            private set => _exitDateTime = value;
        }

        /// <summary>The exit code.</summary>
        public uint ExitCode
        {
            get => _exitCode;
            private set => _exitCode = value;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _processName);
            translator.Translate(ref _processId);
            translator.Translate(ref _parentProcessId);
            translator.Translate(ref _creationDateTime);
            translator.Translate(ref _exitDateTime);
            translator.Translate(ref _exitCode);
        }
    }
}
