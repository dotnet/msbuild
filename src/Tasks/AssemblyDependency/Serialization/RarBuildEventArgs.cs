// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class RarBuildEventArgs : ITranslatable
    {
        private RarBuildEventArgsType _eventType;
        private string? _subcategory;
        private string? _code;
        private string? _file;
        private int _lineNumber;
        private int _columnNumber;
        private int _endLineNumber;
        private int _endColumnNumber;
        private string? _message;
        private string? _helpKeyword;
        private string? _senderName;
        private int _importance;
        private long _eventTimestamp;
        private string[]? _messageArgs;

        public RarBuildEventArgsType EventType { get => _eventType; set => _eventType = value; }

        public string? Subcategory { get => _subcategory; set => _subcategory = value; }

        public string? Code { get => _code; set => _code = value; }

        public string? File { get => _file; set => _file = value; }

        public int LineNumber { get => _lineNumber; set => _lineNumber = value; }

        public int ColumnNumber { get => _columnNumber; set => _columnNumber = value; }

        public int EndLineNumber { get => _endLineNumber; set => _endLineNumber = value; }

        public int EndColumnNumber { get => _endColumnNumber; set => _endColumnNumber = value; }

        public string? Message { get => _message; set => _message = value; }

        public string? HelpKeyword { get => _helpKeyword; set => _helpKeyword = value; }

        public string? SenderName { get => _senderName; set => _senderName = value; }

        public int Importance { get => _importance; set => _importance = value; }

        public long EventTimestamp { get => _eventTimestamp; set => _eventTimestamp = value; }

        public string[]? MessageArgs { get => _messageArgs; set => _messageArgs = value; }

        public void Translate(ITranslator translator)
        {
            translator.TranslateEnum(ref _eventType, (int)_eventType);
            translator.Translate(ref _subcategory);
            translator.Translate(ref _code);
            translator.Translate(ref _file);
            translator.Translate(ref _lineNumber);
            translator.Translate(ref _columnNumber);
            translator.Translate(ref _endLineNumber);
            translator.Translate(ref _endColumnNumber);
            translator.Translate(ref _message);
            translator.Translate(ref _helpKeyword);
            translator.Translate(ref _senderName);
            translator.Translate(ref _importance);
            translator.Translate(ref _eventTimestamp);
            translator.Translate(ref _messageArgs);
        }
    }
}