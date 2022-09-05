// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class SuppressionFileHelper
    {
        /// <summary>
        /// Write the suppression file to disk and throw if a path isn't provided.
        /// </summary>
        public static void GenerateSuppressionFile(ISuppressionEngine suppressionEngine,
            ICompatibilityLogger log,
            string? suppressionFile)
        {
            if (suppressionFile == null)
            {
                throw new ArgumentException(CommonResources.SuppressionsFileNotSpecified, nameof(suppressionFile));
            }

            if (suppressionEngine.WriteSuppressionsToFile(suppressionFile))
            {
                log.LogMessage(MessageImportance.High, CommonResources.WroteSuppressions, suppressionFile);
            }
        }
    }
}
