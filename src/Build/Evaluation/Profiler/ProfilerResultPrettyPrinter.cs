// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Pretty prints a profiled result to a variety of formats
    /// </summary>
    internal sealed class ProfilerResultPrettyPrinter
    {
        /// <summary>
        /// Gets a profiled result in a markdown-like form.
        /// </summary>
        public static string GetMarkdownContent(ProfilerResult result)
        {
            return GetContent(result, new EvaluationLocationMarkdownPrettyPrinter());
        }

        /// <summary>
        /// Gets a profiled result in a tab separated value form.
        /// </summary>
        public static string GetTsvContent(ProfilerResult result)
        {
            return GetContent(result, new EvaluationLocationTabSeparatedPrettyPrinter());
        }

        private static string GetContent(ProfilerResult result, EvaluationLocationPrettyPrinterBase evaluationLocationPrinter)
        {
            var stringBuilder = new StringBuilder();

            evaluationLocationPrinter.AppendHeader(stringBuilder);

            var profiledLocations = result.ProfiledLocations;

            // If there are no profiled locations, then just return
            if (profiledLocations.Count == 0)
            {
                return stringBuilder.ToString();
            }

            var evaluationPasses = profiledLocations.Where(l => l.Key.IsEvaluationPass)
                                                  .OrderBy(l => l.Key.EvaluationPass);

            var orderedLocations = profiledLocations.Where(l => !l.Key.IsEvaluationPass)
                                                  .OrderByDescending(l => l.Value.ExclusiveTime);

            // All evaluation passes go first
            TimeSpan? totalTime = null;
            foreach (var pair in evaluationPasses)
            {
                var time = pair.Value;
                var location = pair.Key;

                if (totalTime == null)
                {
                    totalTime = time.InclusiveTime;
                }

                evaluationLocationPrinter.AppendLocation(stringBuilder, totalTime.Value, location, time);
            }

            Debug.Assert(totalTime != null, "There should be at least one evaluation pass result");

            // All non-evaluation passes go later
            foreach (var pair in orderedLocations)
            {
                var time = pair.Value;
                var location = pair.Key;

                evaluationLocationPrinter.AppendLocation(stringBuilder, totalTime.Value, location, time);
            }

            return stringBuilder.ToString();
        }
    }
}
