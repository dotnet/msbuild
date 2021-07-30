
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal static class IncrementalValuesProviderExtensions
    {
        internal static IncrementalValueProvider<T> WithLambdaComparer<T>(this IncrementalValueProvider<T> source, Func<T, T, bool> equal, Func<T, int> getHashCode)
        {
            var comparer = new LambdaComparer<T>(equal, getHashCode);
            return source.WithComparer(comparer);
        }

        internal static IncrementalValuesProvider<TSource> ReportDiagnostics<TSource>(this IncrementalValuesProvider<(TSource?, Diagnostic?)> source, IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(source, (spc, source) =>
            {
                var (sourceItem, diagnostic) = source;
                if (sourceItem == null && diagnostic != null)
                {
                    spc.ReportDiagnostic(diagnostic);
                }
            });

            return source.Where((pair) => pair.Item1 != null).Select((pair, ct) => pair.Item1!);
        }

        internal static IncrementalValueProvider<TSource> ReportDiagnostics<TSource>(this IncrementalValueProvider<(TSource?, Diagnostic?)> source, IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(source, (spc, source) =>
            {
                var (sourceItem, diagnostic) = source;
                if (sourceItem == null && diagnostic != null)
                {
                    spc.ReportDiagnostic(diagnostic);
                }
            });

            return source.Select((pair, ct) => pair.Item1!);
        }
    }

    internal class LambdaComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> _equal;
        private readonly Func<T, int> _getHashCode;

        public LambdaComparer(Func<T, T, bool> equal, Func<T, int> getHashCode)
        {
            _equal = equal;
            _getHashCode = getHashCode;
        }

        public bool Equals(T x, T y) => _equal(x, y);

        public int GetHashCode(T obj) => _getHashCode(obj);
    }
}
