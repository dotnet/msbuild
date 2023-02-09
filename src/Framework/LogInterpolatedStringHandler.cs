// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework
{
    [InterpolatedStringHandler]
    public ref struct LogInterpolatedStringHandler
    {
        private readonly char[] buffer;
        private int position = 0;
        private int argPosition = 0;

        public object[] Arguments { get; } = Array.Empty<object>();

        public LogInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            if (formattedCount > 99)
            {
                throw new ArgumentOutOfRangeException("Number of formatted arguments must be less than 100.");
            }

            // Buffer size is computed with reserved space for "{x}" and "{xx}" placeholders
            int bufferSize = formattedCount < 10 ?
                literalLength + (3 * formattedCount) :
                literalLength + 3 * (formattedCount % 10) + 4 * (formattedCount - (formattedCount % 10));

            buffer = new char[bufferSize];

            if (formattedCount > 0)
            {
                Arguments = new object[formattedCount];
            }
        }

        public void AppendLiteral(string s)
        {
            s.AsSpan().CopyTo(buffer.AsSpan().Slice(position));
            position += s.Length;
        }

        public void AppendFormatted<T>(T t)
        {
            string indexString = argPosition.ToString();
            buffer[position++] = '{';
            indexString.AsSpan().CopyTo(buffer.AsSpan().Slice(position));
            position += indexString.Length;
            buffer[position++] = '}';

            Arguments[argPosition++] = t;
        }

        internal string GetFormat()
        {
            string result = new string(buffer, 0, position);

            return result;
        }
    }
}

#endif
