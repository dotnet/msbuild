// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

#nullable disable

namespace Microsoft.Build.Framework
{
    internal enum EvaluationPathProbeKind
    {
        File,
        Directory,
        FileOrDirectory,
    }

    internal interface IEvaluationInputObserver
    {
        bool RetainDetails { get; }
        void RecordPathProbe(string path, EvaluationPathProbeKind kind, bool exists);
        void RecordAmbiguousPathProbe(string path, EvaluationPathProbeKind kind);
        void RecordItemMetadata(string itemSpec, string modifier, string baseDirectory, string value);
        void RecordPathAdjustment(string value, string baseDirectory, string result);
        void RecordPathResolution(
            string operation,
            string firstInput,
            string secondInput,
            string firstResult,
            string secondResult);
        void RecordSearch(
            string kind,
            string request,
            IReadOnlyList<string> candidates,
            int candidateCount,
            string candidatesFingerprint,
            string selected);
    }

    internal struct EvaluationInputFingerprintBuilder
    {
        private const ulong Offset1 = 14695981039346656037UL;
        private const ulong Offset2 = 7809847782465536322UL;
        private const ulong Prime1 = 1099511628211UL;
        private const ulong Prime2 = 14029467366897019727UL;

        private ulong _hash1;
        private ulong _hash2;
        private bool _initialized;

        internal void Add(string value)
        {
            EnsureInitialized();
            if (value is null)
            {
                AppendValue(uint.MaxValue);
                return;
            }

            AppendValue((uint)value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                AppendValue(value[i]);
            }
        }

        internal string Complete()
        {
            EnsureInitialized();
            return string.Concat(
                _hash1.ToString("x16", CultureInfo.InvariantCulture),
                _hash2.ToString("x16", CultureInfo.InvariantCulture));
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                _hash1 = Offset1;
                _hash2 = Offset2;
                _initialized = true;
            }
        }

        private void AppendValue(uint value)
        {
            _hash1 = (_hash1 ^ value) * Prime1;
            _hash2 = (_hash2 ^ value) * Prime2;
        }
    }

    internal static class EvaluationInputObserver
    {
        [ThreadStatic]
        private static IEvaluationInputObserver s_current;

        internal static IEvaluationInputObserver Current => s_current;

        internal static IDisposable Enter(IEvaluationInputObserver observer)
        {
            IEvaluationInputObserver previous = s_current;
            s_current = observer;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly IEvaluationInputObserver _previous;
            private int _disposed;

            internal Scope(IEvaluationInputObserver previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    s_current = _previous;
                }
            }
        }
    }
}
