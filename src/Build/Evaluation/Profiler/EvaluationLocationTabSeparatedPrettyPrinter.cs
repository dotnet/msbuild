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
    /// Pretty prints an evaluation location in tab separated value (TSV) format
    /// </summary>
    internal sealed class EvaluationLocationTabSeparatedPrettyPrinter : EvaluationLocationPrettyPrinterBase
    {
        /// <inheritdoc/> 
        internal override void AppendHeader(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine("Id\tParentId\tPass\tFile\tLine #\tExpression\tInc (ms)\tInc (%)\tExc (ms)\tExc (%)\t#\tKind\tBug");
        }

        /// <inheritdoc/>
        internal override void AppendLocation(StringBuilder stringBuilder, TimeSpan totalTime, EvaluationLocation evaluationLocation, ProfiledLocation profiledLocation)
        {
            stringBuilder.AppendLine(string.Join("\t",
                evaluationLocation.Id,
                evaluationLocation.ParentId?.ToString() ?? string.Empty,
                evaluationLocation.EvaluationPassDescription,
                evaluationLocation.File == null ? string.Empty : System.IO.Path.GetFileName(evaluationLocation.File), // file names shouldn't have tabs on any reasonable file system
                evaluationLocation.Line?.ToString() ?? string.Empty,
                NormalizeExpression(evaluationLocation.ElementDescription, evaluationLocation.Kind) ?? string.Empty,
                GetMilliseconds(profiledLocation.InclusiveTime),
                GetPercentage(totalTime, profiledLocation.InclusiveTime) + "%",
                GetMilliseconds(profiledLocation.ExclusiveTime),
                GetPercentage(totalTime, profiledLocation.ExclusiveTime) + "%",
                profiledLocation.NumberOfHits,
                evaluationLocation.Kind + "\t"));
        }

        /// <inheritdoc/>
        protected override string NormalizeExpression(string description, EvaluationLocationKind kind)
        {
            var text = GetElementOrConditionText(description, kind);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            // Swap tabs for spaces, so we don't mess up the TSV format
            text = text.Replace('\t', ' ');

            return '`' + text + '`';
        }
    }
}
