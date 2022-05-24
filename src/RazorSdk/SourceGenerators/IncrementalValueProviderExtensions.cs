
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal static class IncrementalValuesProviderExtensions
    {
        /// <summary>
        /// Adds a comparer that will force the provider to be considered as cached if the razor options call for suppression
        /// </summary>
        internal static IncrementalValueProvider<T> AsCachedIfSuppressed<T>(this IncrementalValueProvider<T> provider, IncrementalValueProvider<bool> isSuppressedProvider)
            where T : notnull
        {
            return provider.Combine(isSuppressedProvider).WithComparer(new RazorSourceGeneratorComparer<T>()).Select((pair, _) => pair.Left);
        }

        /// <summary>
        /// Adds a comparer that will force the provider to be considered as cached if the razor options call for suppression
        /// </summary>
        internal static IncrementalValuesProvider<T> AsCachedIfSuppressed<T>(this IncrementalValuesProvider<T> provider, IncrementalValueProvider<bool> isSuppressedProvider)
            where T : notnull
        {
            return provider.Combine(isSuppressedProvider).WithComparer(new RazorSourceGeneratorComparer<T>()).Select((pair, _) => pair.Left);
        }


        internal static IncrementalValueProvider<T> WithLambdaComparer<T>(this IncrementalValueProvider<T> source, Func<T, T, bool> equal, Func<T, int> getHashCode)
        {
            var comparer = new LambdaComparer<T>(equal, getHashCode);
            return source.WithComparer(comparer);
        }

        internal static IncrementalValuesProvider<T> WithLambdaComparer<T>(this IncrementalValuesProvider<T> source, Func<T, T, bool> equal, Func<T, int> getHashCode)
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

        /// <summary>
        /// A highly specialized comparer that allows for treating an event source as cached if the razor options are set to suppress generation.
        /// </summary>
        /// <remarks>
        /// This should not be used outside of <see cref="IncrementalValuesProviderExtensions.AsCachedIfSuppressed{T}(IncrementalValueProvider{T}, IncrementalValueProvider{RazorSourceGenerationOptions})"/>
        /// </remarks>
        private sealed class RazorSourceGeneratorComparer<T> : IEqualityComparer<(T Left, bool IsSuppressed)> where T : notnull
        {
            private readonly Func<T, T, bool> _equals;
            public RazorSourceGeneratorComparer(Func<T, T, bool>? equals = null)
            {
                _equals = equals ?? EqualityComparer<T>.Default.Equals;
            }

            public bool Equals((T Left, bool IsSuppressed) x, (T Left, bool IsSuppressed) y)
            {
                if (y.IsSuppressed)
                {
                    // If source generation is suppressed, we can always use previously cached results.
                    return true;
                }

                return _equals(x.Left, y.Left);
            }

            public int GetHashCode((T Left, bool IsSuppressed) obj) => throw new NotImplementedException("GetHashCode is never expected to be called");
        }
    }

    internal sealed class LambdaComparer<T> : IEqualityComparer<T>
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
