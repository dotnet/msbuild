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
    /// Pretty prints an evaluation location in markdown format
    /// </summary>
    internal sealed class EvaluationLocationMarkdownPrettyPrinter : EvaluationLocationPrettyPrinterBase
    {
        /// <inheritdoc/>
        internal override void AppendHeader(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine("Id|ParentId|Pass|File|Line #|Expression|Inc (ms)|Inc (%)|Exc (ms)|Exc (%)|#|Kind|Bug");
            stringBuilder.AppendLine("---|---|---|---|---:|---|---:|---:|---:|---:|---:|---:|---");
        }

        /// <inheritdoc/>
        internal override void AppendLocation(StringBuilder stringBuilder, TimeSpan totalTime, EvaluationLocation evaluationLocation, ProfiledLocation profiledLocation)
        {
            stringBuilder.AppendLine(string.Join("|",
                evaluationLocation.Id,
                evaluationLocation.ParentId?.ToString() ?? string.Empty,
                evaluationLocation.EvaluationPassDescription,
                evaluationLocation.File == null ? string.Empty : System.IO.Path.GetFileName(evaluationLocation.File),
                evaluationLocation.Line?.ToString() ?? string.Empty,
                NormalizeExpression(evaluationLocation.ElementDescription, evaluationLocation.Kind) ?? string.Empty,
                GetMilliseconds(profiledLocation.InclusiveTime),
                GetPercentage(totalTime, profiledLocation.InclusiveTime) + "%",
                GetMilliseconds(profiledLocation.ExclusiveTime),
                GetPercentage(totalTime, profiledLocation.ExclusiveTime) + "%",
                profiledLocation.NumberOfHits,
                evaluationLocation.Kind + "|"));
        }

        /// <inheritdoc/>
        protected override string NormalizeExpression(string description, EvaluationLocationKind kind)
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
    }
}
