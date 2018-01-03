// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Text;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Pretty prints an evaluation location with its associated profiled location
    /// </summary>
    internal abstract class EvaluationLocationPrettyPrinterBase
    {
        /// <summary>
        /// Appends the header of all the locations to the string builder
        /// </summary>
        /// <param name="stringBuilder"></param>
        internal abstract void AppendHeader(StringBuilder stringBuilder);

        /// <summary>
        /// Appends a pretty printed location with its associated profiled data
        /// </summary>
        internal abstract void AppendLocation(StringBuilder stringBuilder, TimeSpan totalTime, EvaluationLocation evaluationLocation, ProfiledLocation profiledLocation);

        /// <summary>
        /// Normalizes the expression returned by <see cref="GetElementOrConditionText"/>
        /// </summary>
        protected abstract string NormalizeExpression(string description, EvaluationLocationKind kind);

        /// <nodoc/>
        protected static double GetMilliseconds(TimeSpan timeSpan)
        {
            return Math.Round(timeSpan.TotalMilliseconds, 0, MidpointRounding.AwayFromZero);
        }

        /// <nodoc/>
        protected static double GetPercentage(TimeSpan total, TimeSpan time)
        {
            var percentage = (time.TotalMilliseconds / total.TotalMilliseconds) * 100;

            return Math.Round(percentage, 1, MidpointRounding.AwayFromZero);
        }

        /// <nodoc/>
        protected static string GetElementOrConditionText(string description, EvaluationLocationKind kind)
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

            var newLineIndex = outerXml.IndexOfAny(new[] { '\r', '\n' });
            return newLineIndex == -1 ? outerXml : outerXml.Remove(newLineIndex);
        }
    }
}
