// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Transforms a profiled result to markdown form.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Transforms a profiled result to markdown form
    /// </summary>
    public sealed class ProfilerResultPrettyPrinter
    {
        /// <summary>
        /// Gets a profiled result in a markdown-like form.
        /// </summary>
        public static string GetMarkdownContent(ProfilerResult result)
        {
            var profiledLocations = result.ProfiledLocations;
            var evaluationPasses = profiledLocations.Where(l => l.Key.File == null)
                                                  .OrderBy(l => l.Key.EvaluationPass);

            var orderedLocations = profiledLocations.Where(l => l.Key.File != null)
                                                  .OrderByDescending(l => l.Value.ExclusiveTime);

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Pass|File|Line #|Expression|Inc (ms)|Inc (%)|Exc (ms)|Exc (%)|#|Bug");
            stringBuilder.AppendLine("---|---|---:|---|---:|---:|---:|---:|---:|---");

            TimeSpan? totalTime = null;
            foreach (var pair in evaluationPasses)
            {
                var time = pair.Value;
                var location = pair.Key;

                if (totalTime == null)
                {
                    totalTime = time.InclusiveTime;
                }

                stringBuilder.AppendLine(string.Join("|",
                    location.EvaluationDescription,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    GetMilliseconds(time.InclusiveTime),
                    GetPercentage(totalTime.Value, time.InclusiveTime) + "%",
                    GetMilliseconds(time.ExclusiveTime),
                    GetPercentage(totalTime.Value, time.ExclusiveTime) + "%",
                    time.NumberOfHits + "|"));
            }

            Debug.Assert(totalTime != null, "There should be at least one evaluation pass result");

            foreach (var pair in orderedLocations)
            {
                var time = pair.Value;
                var location = pair.Key;

                if (time.InclusiveTime.TotalMilliseconds < 1 || time.ExclusiveTime.TotalMilliseconds < 1)
                    continue;

                stringBuilder.AppendLine(string.Join("|",
                    location.EvaluationDescription,
                    location.File == null ? string.Empty : Path.GetFileName(location.File),
                    location.Line?.ToString() ?? string.Empty,
                    GetExpression(location.ElementOrCondition, location.IsElement),
                    GetMilliseconds(time.InclusiveTime),
                    GetPercentage(totalTime.Value, time.InclusiveTime) + "%",
                    GetMilliseconds(time.ExclusiveTime),
                    GetPercentage(totalTime.Value, time.ExclusiveTime) + "%",
                    time.NumberOfHits + "|"));
            }

            return stringBuilder.ToString();
        }

        private static double GetMilliseconds(TimeSpan timeSpan)
        {
            return Math.Round(timeSpan.TotalMilliseconds, 0, MidpointRounding.AwayFromZero);
        }

        private static double GetPercentage(TimeSpan total, TimeSpan time)
        {
            var percentage = (time.TotalMilliseconds / total.TotalMilliseconds) * 100;

            return Math.Round(percentage, 1, MidpointRounding.AwayFromZero);
        }

        private static string GetExpression(string elementOrCondition, bool isElement)
        {
            var text = GetElementOrConditionText(elementOrCondition, isElement);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            text = text.Replace("|", "\\|");

            if (text.Length > 100)
                text = text.Remove(100) + "...";

            return '`' + text + '`';
        }

        private static string GetElementOrConditionText(string elementOrCondition, bool isElement)
        {
            if (elementOrCondition == null)
            {
                return null;
            }

            if (!isElement)
            {
                return $"Condition=\"{elementOrCondition}\")";
            }

            var outerXml = elementOrCondition;
            outerXml = outerXml.Replace(@"xmlns=""http://schemas.microsoft.com/developer/msbuild/2003""", "");

            var newLineIndex = outerXml.IndexOfAny(new [] { '\r', '\n' });
            return newLineIndex == -1 ? outerXml : outerXml.Remove(newLineIndex);
        }
    }
}
