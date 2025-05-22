// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework.Profiler;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Pretty prints an evaluation location in markdown format
    /// </summary>
    internal sealed class EvaluationLocationMarkdownPrettyPrinter : EvaluationLocationPrettyPrinterBase
    {
        private const string Separator = "|";

        /// <inheritdoc/>
        internal override void AppendHeader(StringBuilder stringBuilder)
        {
            AppendDefaultHeaderWithSeparator(stringBuilder, Separator);
            stringBuilder.AppendLine("---|---|---|---|---:|---|---:|---:|---:|---:|---:|---:|---");
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

            text = text.Replace(Separator, $"\\{Separator}");

            if (text.Length > 100)
            {
                text =
#if NET
                    $"{text.AsSpan(0, 100)}...";
#else
                    $"{text.Remove(100)}...";
#endif
            }

            return $"`{text}`";
        }
    }
}
