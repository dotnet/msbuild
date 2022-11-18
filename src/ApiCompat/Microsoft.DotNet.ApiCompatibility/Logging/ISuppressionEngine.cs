// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Collection of Suppressions which is able to add suppressions, check if a specific error is suppressed, and write all suppressions
    /// down to a file. The engine is thread-safe.
    /// </summary>
    public interface ISuppressionEngine
    {
        bool BaselineAllErrors { get; }

        /// <summary>
        /// Checks if the passed in error is suppressed or not.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID of the error to check.</param>
        /// <param name="target">The target of where the <paramref name="diagnosticId"/> should be applied.</param>
        /// <param name="left">Optional. The left operand in a APICompat error.</param>
        /// <param name="right">Optional. The right operand in a APICompat error.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        bool IsErrorSuppressed(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false);

        /// <summary>
        /// Checks if the passed in error is suppressed or not.
        /// </summary>
        /// <param name="error">The <see cref="Suppression"/> error to check.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        bool IsErrorSuppressed(Suppression error);

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="diagnosticId">The diagnostic ID of the error to add.</param>
        /// <param name="target">The target of where the <paramref name="diagnosticId"/> should be applied.</param>
        /// <param name="left">Optional. The left operand in a APICompat error.</param>
        /// <param name="right">Optional. The right operand in a APICompat error.</param>
        void AddSuppression(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false);

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="suppression">The <see cref="Suppression"/> to be added.</param>
        void AddSuppression(Suppression suppression);

        /// <summary>
        /// Writes all suppressions in collection down to a file, if empty it doesn't write anything.
        /// </summary>
        /// <param name="supressionFile">The path to the file to be written.</param>
        /// <returns>Whether it wrote the file.</returns>
        bool WriteSuppressionsToFile(string suppressionOutputFile);
    }
}
