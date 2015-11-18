// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.DotNet.Cli.Utils.CommandParsing
{
    internal struct Cursor
    {
        private readonly string _text;
        private readonly int _start;
        private readonly int _end;

        public Cursor(string text, int start, int end)
        {
            _text = text;
            _start = start;
            _end = end;
        }

        public bool IsEnd
        {
            get { return _start == _end; }
        }

        public char Peek(int index)
        {
            return (index + _start) >= _end ? (char)0 : _text[index + _start];
        }

        public Result<TValue> Advance<TValue>(TValue result, int length)
        {
            return new Result<TValue>(result, new Cursor(_text, _start + length, _end));
        }
    }
}