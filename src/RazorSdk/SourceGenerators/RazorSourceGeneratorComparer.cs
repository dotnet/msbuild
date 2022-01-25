// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    /// <summary>
    /// A comparer used to determine if an incremental steps needs to be re-run accounting for <see cref="RazorSourceGenerationOptions.SuppressRazorSourceGenerator"/>.
    /// <para>
    /// In VS, design time builds are executed with <see cref="RazorSourceGenerationOptions.SuppressRazorSourceGenerator"/> set to <c>true</c>. In this case, RSG can safely
    /// allow previously cached results to be used, while no-oping in the step that adds sources to the context. This allows source generator caches from being evicted
    /// when the value of this property flip-flips during a hot-reload / EnC session.
    /// </para>
    /// </summary>
    internal sealed class RazorSourceGeneratorComparer<T> : IEqualityComparer<(T Left, RazorSourceGenerationOptions Right)> where T : notnull
    {
        private readonly Func<T, T, bool> _equals;
        public RazorSourceGeneratorComparer(Func<T, T, bool>? equals = null)
        {
            _equals = equals ?? EqualityComparer<T>.Default.Equals;
        }

        public bool Equals((T Left, RazorSourceGenerationOptions Right) x, (T Left, RazorSourceGenerationOptions Right) y)
        {
            if (y.Right.SuppressRazorSourceGenerator)
            {
                // If source generation is suppressed, we can always use previously cached results.
                return true;
            }

            return _equals(x.Left, y.Left) && x.Right.EqualsIgnoringSupression(y.Right);
        }

        public int GetHashCode((T Left, RazorSourceGenerationOptions Right) obj) => obj.Left.GetHashCode();
    }
}
