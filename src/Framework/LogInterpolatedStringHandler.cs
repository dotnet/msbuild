// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents interpolation string handler which allows to get string format and parameters
    /// such like <see cref="FormattableString"/>.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct LogInterpolatedStringHandler
    {
        private readonly char[] buffer;
        private int position = 0;
        private int argPosition = 0;

        public readonly object?[] Arguments { get; } = Array.Empty<object?>();

        public LogInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            // Buffer size is computed with reserved space for "{x..x}" placeholders
            int maxNumberOfDigits = GetNumberOfDigits(formattedCount);
            int bufferSize = literalLength + (formattedCount * (maxNumberOfDigits + 2));

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

        private static int GetNumberOfDigits(int value)
        {
            // It's OK to return 0 if the value is 0, because we don't need to reserve
            // extra space in that case
            int result = 0;

            while (value > 0)
            {
                result++;
                value /= 10;
            }

            return result;
        }
    }
}

#endif
