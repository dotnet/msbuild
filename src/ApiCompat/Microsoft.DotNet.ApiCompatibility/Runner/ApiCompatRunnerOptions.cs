// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
