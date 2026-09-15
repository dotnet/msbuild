// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Transport-only representation of a task's reported file access.
    /// </summary>
    internal struct TaskHostFileAccessData
    {
        private int _operation;
        private int _requestedAccess;
        private uint _processId;
        private uint _id;
        private uint _correlationId;
        private uint _error;
        private uint _desiredAccess;
        private uint _flagsAndAttributes;
        private string _path;
        private string _processArgs;
        private bool _isAnAugmentedFileAccess;
        private string _enumeratePattern;
        private uint _openedFileOrDirectoryAttributes;

        internal TaskHostFileAccessData(
            int operation,
            int requestedAccess,
            uint processId,
            uint id,
            uint correlationId,
            uint error,
            uint desiredAccess,
            uint flagsAndAttributes,
            string path,
            string processArgs,
            bool isAnAugmentedFileAccess,
            string enumeratePattern,
            uint openedFileOrDirectoryAttributes)
        {
            _operation = operation;
            _requestedAccess = requestedAccess;
            _processId = processId;
            _id = id;
            _correlationId = correlationId;
            _error = error;
            _desiredAccess = desiredAccess;
            _flagsAndAttributes = flagsAndAttributes;
            _path = path;
            _processArgs = processArgs;
            _isAnAugmentedFileAccess = isAnAugmentedFileAccess;
            _enumeratePattern = enumeratePattern;
            _openedFileOrDirectoryAttributes = openedFileOrDirectoryAttributes;
        }

        internal int Operation => _operation;

        internal int RequestedAccess => _requestedAccess;

        internal uint ProcessId => _processId;

        internal uint Id => _id;

        internal uint CorrelationId => _correlationId;

        internal uint Error => _error;

        internal uint DesiredAccess => _desiredAccess;

        internal uint FlagsAndAttributes => _flagsAndAttributes;

        internal string Path => _path;

        internal string ProcessArgs => _processArgs;

        internal bool IsAnAugmentedFileAccess => _isAnAugmentedFileAccess;

        internal string EnumeratePattern => _enumeratePattern;

        internal uint OpenedFileOrDirectoryAttributes => _openedFileOrDirectoryAttributes;

        internal void Translate(ITranslator translator)
        {
            translator.Translate(ref _operation);
            translator.Translate(ref _requestedAccess);
            translator.Translate(ref _processId);
            translator.Translate(ref _id);
            translator.Translate(ref _correlationId);
            translator.Translate(ref _error);
            translator.Translate(ref _desiredAccess);
            translator.Translate(ref _flagsAndAttributes);
            translator.Translate(ref _path);
            translator.Translate(ref _processArgs);
            translator.Translate(ref _isAnAugmentedFileAccess);
            translator.Translate(ref _enumeratePattern);
            translator.Translate(ref _openedFileOrDirectoryAttributes);
        }
    }
}
