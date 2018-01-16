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
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Id|ParentId|Pass|File|Line #|Expression|Inc (ms)|Inc (%)|Exc (ms)|Exc (%)|#|Kind|Bug");
            stringBuilder.AppendLine("---|---|---|---|---:|---|---:|---:|---:|---:|---:|---:|---");

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
                    location.Id,
                    location.ParentId?.ToString() ?? string.Empty,
                    location.EvaluationPassDescription,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    GetMilliseconds(time.InclusiveTime),
                    GetPercentage(totalTime.Value, time.InclusiveTime) + "%",
                    GetMilliseconds(time.ExclusiveTime),
                    GetPercentage(totalTime.Value, time.ExclusiveTime) + "%",
                    time.NumberOfHits,
                    location.Kind + "|"));
            }

            Debug.Assert(totalTime != null, "There should be at least one evaluation pass result");

            foreach (var pair in orderedLocations)
            {
                var time = pair.Value;
                var location = pair.Key;

                stringBuilder.AppendLine(string.Join("|",
                    location.Id,
                    location.ParentId?.ToString() ?? string.Empty,
                    location.EvaluationPassDescription,
                    location.File == null ? string.Empty : Path.GetFileName(location.File),
                    location.Line?.ToString() ?? string.Empty,
                    GetExpression(location.ElementDescription, location.Kind),
                    GetMilliseconds(time.InclusiveTime),
                    GetPercentage(totalTime.Value, time.InclusiveTime) + "%",
                    GetMilliseconds(time.ExclusiveTime),
                    GetPercentage(totalTime.Value, time.ExclusiveTime) + "%",
                    time.NumberOfHits,
                    location.Kind + "|"));
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

        private static string GetExpression(string description, EvaluationLocationKind kind)
        {
            var text = GetElementOrConditionText(description, kind);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            text = text.Replace("|", "\\|");

            if (text.Length > 100)
                text = text.Remove(100) + "...";

            return '`' + text + '`';
        }

        private static string GetElementOrConditionText(string description, EvaluationLocationKind kind)
        {
            if (description == null)
            {
                return null;
            }

            if (kind == EvaluationLocationKind.Condition)
            {
                return $"Condition=\"{description}\")";
            }

            if (kind == EvaluationLocationKind.Glob)
            {
                return $"Glob=\"{description}\")";
            }

            var outerXml = description;
            outerXml = outerXml.Replace(@"xmlns=""http://schemas.microsoft.com/developer/msbuild/2003""", "");

            var newLineIndex = outerXml.IndexOfAny(new [] { '\r', '\n' });
            return newLineIndex == -1 ? outerXml : outerXml.Remove(newLineIndex);
        }
    }
}
