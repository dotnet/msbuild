// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework.Profiler;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Pretty prints an evaluation location in tab separated value (TSV) format
    /// </summary>
    internal sealed class EvaluationLocationTabSeparatedPrettyPrinter : EvaluationLocationPrettyPrinterBase
    {
        private const string Separator = "\t";

        /// <inheritdoc/>
        internal override void AppendHeader(StringBuilder stringBuilder)
        {
            AppendDefaultHeaderWithSeparator(stringBuilder, Separator);
        }

        /// <inheritdoc/>
        internal override void AppendLocation(StringBuilder stringBuilder, TimeSpan totalTime, EvaluationLocation evaluationLocation, ProfiledLocation profiledLocation)
        {
            AppendDefaultLocationWithSeparator(stringBuilder, totalTime, evaluationLocation, profiledLocation, Separator);
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
            return text.Replace(Separator, " ");
        }
    }
}
