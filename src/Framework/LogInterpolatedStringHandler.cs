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

        public readonly object?[] Arguments { get; } = Array.Empty<object?>();

        public LogInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            int bufferSize;

            // Buffer size is computed with reserved space for "{x..x}" placeholders
            if (formattedCount < 10)
            {
                bufferSize = literalLength + (3 * formattedCount);
            }
            else
            {
                int maxNumberOfDigits = (int)(Math.Log10(formattedCount) + 1);
                bufferSize = literalLength + (formattedCount * (maxNumberOfDigits + 2));
            }

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
            buffer[position++] = '{';

            if (argPosition < 10)
            {
                buffer[position++] = (char)('0' + argPosition);
            }
            else
            {
                string indexString = argPosition.ToString();
                indexString.AsSpan().CopyTo(buffer.AsSpan().Slice(position));
                position += indexString.Length;
            }

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
