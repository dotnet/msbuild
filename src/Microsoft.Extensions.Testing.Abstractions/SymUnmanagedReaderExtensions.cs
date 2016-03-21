// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.DiaSymReader;
using System.Linq;

namespace Microsoft.Extensions.Testing.Abstractions
{
    internal static class SymUnmanagedReaderExtensions
    {
        internal const int E_FAIL = unchecked((int)0x80004005);
        internal const int E_NOTIMPL = unchecked((int)0x80004001);

        private static readonly IntPtr s_ignoreIErrorInfo = new IntPtr(-1);
        private delegate int ItemsGetter<TEntity, TItem>(TEntity entity, int bufferLength, out int count, TItem[] buffer);

        internal static void ThrowExceptionForHR(int hr)
        {
            // E_FAIL indicates "no info".
            // E_NOTIMPL indicates a lack of ISymUnmanagedReader support (in a particular implementation).
            if (hr < 0 && hr != E_FAIL && hr != E_NOTIMPL)
            {
                Marshal.ThrowExceptionForHR(hr, s_ignoreIErrorInfo);
            }
        }

        internal static SourceInformation GetSourceInformation(this ISymUnmanagedMethod method)
        {
            var sequencePoint = method.GetSequencePoints().OrderBy(s => s.StartLine).FirstOrDefault();
            var fileName = sequencePoint.Document.GetName();
            var lineNumber = sequencePoint.StartLine;

            return new SourceInformation(fileName, lineNumber);
        }

        internal static ImmutableArray<SymUnmanagedSequencePoint> GetSequencePoints(this ISymUnmanagedMethod method)
        {
            // NB: method.GetSequencePoints(0, out numAvailable, ...) always returns 0.
            int numAvailable;
            int hr = method.GetSequencePointCount(out numAvailable);
            SymUnmanagedReaderExtensions.ThrowExceptionForHR(hr);
            if (numAvailable == 0)
            {
                return ImmutableArray<SymUnmanagedSequencePoint>.Empty;
            }

            int[] offsets = new int[numAvailable];
            ISymUnmanagedDocument[] documents = new ISymUnmanagedDocument[numAvailable];
            int[] startLines = new int[numAvailable];
            int[] startColumns = new int[numAvailable];
            int[] endLines = new int[numAvailable];
            int[] endColumns = new int[numAvailable];

            int numRead;
            hr = method.GetSequencePoints(numAvailable, out numRead, offsets, documents, startLines, startColumns, endLines, endColumns);
            SymUnmanagedReaderExtensions.ThrowExceptionForHR(hr);
            if (numRead != numAvailable)
            {
                throw new InvalidOperationException($"Read only {numRead} of {numAvailable} sequence points.");
            }

            var builder = ImmutableArray.CreateBuilder<SymUnmanagedSequencePoint>(numRead);
            for (int i = 0; i < numRead; i++)
            {
                builder.Add(new SymUnmanagedSequencePoint(
                    offsets[i],
                    documents[i],
                    startLines[i],
                    startColumns[i],
                    endLines[i],
                    endColumns[i]));
            }

            return builder.ToImmutable();
        }

        internal static string GetName(this ISymUnmanagedDocument document)
        {
            return ToString(GetItems(document,
                (ISymUnmanagedDocument a, int b, out int c, char[] d) => a.GetUrl(b, out c, d)));
        }

        private static TItem[] GetItems<TEntity, TItem>(TEntity entity, ItemsGetter<TEntity, TItem> getter)
        {
            int count;
            int hr = getter(entity, 0, out count, null);
            ThrowExceptionForHR(hr);
            if (count == 0)
            {
                return null;
            }

            var result = new TItem[count];
            hr = getter(entity, count, out count, result);
            ThrowExceptionForHR(hr);
            ValidateItems(count, result.Length);
            return result;
        }

        private static void ValidateItems(int actualCount, int bufferLength)
        {
            if (actualCount != bufferLength)
            {
                throw new InvalidOperationException(string.Format("Read only {0} of {1} items.", actualCount, bufferLength));
            }
        }

        private static string ToString(char[] buffer)
        {
            return new string(buffer, 0, buffer.Length - 1);
        }
    }
}
