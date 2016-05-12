// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel
{
    public class AnalyzerOptions : IEquatable<AnalyzerOptions>
    {
        /// <summary>
        /// The identifier indicating the project language as defined by NuGet.
        /// </summary>
        /// <remarks>
        /// See https://docs.nuget.org/create/analyzers-conventions for valid values
        /// </remarks>
        public string LanguageId { get; }

        public AnalyzerOptions(string languageId = null)
        {
            LanguageId = languageId;
        }

        public bool Equals(AnalyzerOptions other)
        {
            return !ReferenceEquals(other, null) && other.LanguageId == LanguageId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AnalyzerOptions);
        }

        public override int GetHashCode()
        {
            return LanguageId?.GetHashCode() ?? 0;
        }

        public static bool operator ==(AnalyzerOptions left, AnalyzerOptions right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(AnalyzerOptions left, AnalyzerOptions right)
        {
            return !(left == right);
        }
    }
}