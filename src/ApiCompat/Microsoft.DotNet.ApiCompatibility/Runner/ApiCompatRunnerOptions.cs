// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Options to configure api comparison performed by the <see cref="IApiCompatRunner"/>.
    /// </summary>
    public readonly struct ApiCompatRunnerOptions
    {
        /// <summary>
        /// Performs api comparison in strict mode if true.
        /// </summary>
        public readonly bool EnableStrictMode;

        /// <summary>
        /// True if assemblies from different roots are compared.
        /// </summary>
        public readonly bool IsBaselineComparison;

        /// <summary>
        /// Initializes api compat options
        /// </summary>
        public ApiCompatRunnerOptions(bool enableStrictMode,
            bool isBaselineComparison = false)
        {
            EnableStrictMode = enableStrictMode;
            IsBaselineComparison = isBaselineComparison;
        }

        /// <inheritdoc />
        public override string ToString() => $"options [ strict mode: {EnableStrictMode}, baseline: {IsBaselineComparison} ]";
    }
}
