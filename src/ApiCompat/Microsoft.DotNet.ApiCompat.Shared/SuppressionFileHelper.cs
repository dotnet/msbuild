// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class SuppressionFileHelper
    {
        /// <summary>
        /// Write the suppression file to disk and throw if a path isn't provided.
        /// </summary>
        public static void GenerateSuppressionFile(ISuppressionEngine suppressionEngine,
            ISuppressableLog log,
            string[]? suppressionFiles,
            string? suppressionOutputFile)
        {
            // When a single suppression (input) file is passed in but no suppression output file, use the single input file.
            if (suppressionOutputFile == null && suppressionFiles?.Length == 1)
            {
                suppressionOutputFile = suppressionFiles[0];
            }

            if (suppressionOutputFile == null)
            {
                throw new ArgumentException(CommonResources.SuppressionsFileNotSpecified, nameof(suppressionOutputFile));
            }

            if (suppressionEngine.WriteSuppressionsToFile(suppressionOutputFile))
            {
                log.LogMessage(MessageImportance.High,
                    string.Format(CommonResources.WroteSuppressions,
                        suppressionOutputFile));
            }
        }

        /// <summary>
        /// Log whether or not we found breaking changes. If we are writing to a suppression file, no need to log anything.
        /// </summary>
        public static void LogApiCompatSuccessOrFailure(bool generateSuppressionFile, ISuppressableLog log)
        {
            if (log.HasLoggedErrorSuppressions)
            {
                if (!generateSuppressionFile)
                {
                    log.LogError(Resources.BreakingChangesFoundRegenerateSuppressionFileCommandHelp);
                }
            }
            else
            {
                log.LogMessage(MessageImportance.Normal,
                    CommonResources.NoBreakingChangesFound);
            }
        }
    }
}
