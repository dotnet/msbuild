// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Experimental.FileAccess
{
    /// <summary>
    /// File access data.
    /// </summary>
    [CLSCompliant(false)]
    public struct FileAccessData
        : ITranslatable
    {
        private ReportedFileOperation _operation;
        private RequestedAccess _requestedAccess;
        private uint _processId;
        private uint _id;
        private uint _correlationId;
        private uint _error;
        private DesiredAccess _desiredAccess;
        private FlagsAndAttributes _flagsAndAttributes;
        private string _path;
        private string? _processArgs;
        private bool _isAnAugmentedFileAccess;

        public FileAccessData(
            ReportedFileOperation operation,
            RequestedAccess requestedAccess,
            uint processId,
            uint id,
            uint correlationId,
            uint error,
            DesiredAccess desiredAccess,
            FlagsAndAttributes flagsAndAttributes,
            string path,
            string? processArgs,
            bool isAnAugmentedFileAccess)
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
        }

        /// <summary>The operation that performed the file access.</summary>
        public ReportedFileOperation Operation
        {
            readonly get => _operation;
            private set => _operation = value;
        }

        /// <summary>The requested access.</summary>
        public RequestedAccess RequestedAccess
        {
            readonly get => _requestedAccess;
            private set => _requestedAccess = value;
        }

        /// <summary>The process id.</summary>
        public uint ProcessId
        {
            readonly get => _processId;
            private set => _processId = value;
        }

        /// <summary>Id of file access.</summary>
        public uint Id
        {
            readonly get => _id;
            private set => _id = value;
        }


        /// <summary>Correlation id of file access.</summary>
        public uint CorrelationId
        {
            readonly get => _correlationId;
            private set => _correlationId = value;
        }


        /// <summary>The error code of the operation.</summary>
        public uint Error
        {
            readonly get => _error;
            private set => _error = value;
        }

        /// <summary>The desired access.</summary>
        public DesiredAccess DesiredAccess
        {
            readonly get => _desiredAccess;
            private set => _desiredAccess = value;
        }

        /// <summary>The file flags and attributes.</summary>
        public FlagsAndAttributes FlagsAndAttributes
        {
            readonly get => _flagsAndAttributes;
            private set => _flagsAndAttributes = value;
        }

        /// <summary>The path being accessed.</summary>
        public string Path
        {
            readonly get => _path;
            private set => _path = value;
        }

        /// <summary>The process arguments.</summary>
        public string? ProcessArgs
        {
            readonly get => _processArgs;
            private set => _processArgs = value;
        }

        /// <summary>Whether the file access is augmented.</summary>
        public bool IsAnAugmentedFileAccess
        {
            readonly get => _isAnAugmentedFileAccess;
            private set => _isAnAugmentedFileAccess = value;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _operation, (int)_operation);
            translator.TranslateEnum(ref _requestedAccess, (int)_requestedAccess);
            translator.Translate(ref _processId);
            translator.Translate(ref _id);
            translator.Translate(ref _correlationId);
            translator.Translate(ref _error);
            translator.TranslateEnum(ref _desiredAccess, (int)_desiredAccess);
            translator.TranslateEnum(ref _flagsAndAttributes, (int)_flagsAndAttributes);
            translator.Translate(ref _path);
            translator.Translate(ref _processArgs);
            translator.Translate(ref _isAnAugmentedFileAccess);
        }
    }
}
