// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Suppression engine that contains a collection of <see cref="Suppression"/> items. It provides API to add a suppression, check if a passed-in suppression is already suppressed
    /// and serialize all suppressions down into a file.
    /// </summary>
    public interface ISuppressionEngine
    {
        /// <summary>
        /// If true, adds the suppression to the collection when passed into <see cref="IsErrorSuppressed(Suppression)"/>. 
        /// </summary>
        bool BaselineAllErrors { get; }

        /// <summary>
        /// The baseline suppressions from the passed-in suppression files.
        /// </summary>
        IReadOnlyCollection<Suppression> BaselineSuppressions { get; }

        /// <summary>
        /// The current suppressions for api compatibility differences.
        /// </summary>
        IReadOnlyCollection<Suppression> Suppressions { get; }

        /// <summary>
        /// Adds a suppression to the collection.
        /// </summary>
        /// <param name="suppression">The <see cref="Suppression"/> to be added.</param>
        void AddSuppression(Suppression suppression);

        /// <summary>
        /// Retrieves unnecessary suppressions stored in the suppression file.
        /// </summary>
        /// <returns>Returns unnecessary suppressions.</returns>
        IReadOnlyCollection<Suppression> GetUnnecessarySuppressions();

        /// <summary>
        /// Checks if the passed in error is suppressed.
        /// </summary>
        /// <param name="error">The <see cref="Suppression"/> error to check.</param>
        /// <returns><see langword="true"/> if the error is already suppressed. <see langword="false"/> otherwise.</returns>
        bool IsErrorSuppressed(Suppression error);

        /// <summary>
        /// Load suppressions from suppression files.
        /// </summary>
        /// <param name="suppressionFiles">Suppression files to read from.</param>
        void LoadSuppressions(params string[] suppressionFiles);

        /// <summary>
        /// Writes the suppressions into the provided suppression file path and if empty, skips the operation.
        /// </summary>
        /// <param name="suppressionOutputFile">The path to the file to be written.</param>
        /// <param name="preserveUnnecessarySuppressions">If <see langword="true"/>, preserves unnecessary suppressions.</param>
        /// <returns>Returns the set of suppressions written.</returns>
        IReadOnlyCollection<Suppression> WriteSuppressionsToFile(string suppressionOutputFile, bool preserveUnnecessarySuppressions = false);
    }
}
