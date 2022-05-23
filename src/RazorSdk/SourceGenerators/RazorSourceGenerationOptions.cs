
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal sealed class RazorSourceGenerationOptions : IEquatable<RazorSourceGenerationOptions>
    {
        public string RootNamespace { get; set; } = "ASP";

        public RazorConfiguration Configuration { get; set; } = RazorConfiguration.Default;

        /// <summary>
        /// Gets a flag that determines if generated Razor views and Pages includes the <c>RazorSourceChecksumAttribute</c>.
        /// </summary>
        public bool GenerateMetadataSourceChecksumAttributes { get; set; } = false;

        /// <summary>
        /// Gets the CSharp language version currently used by the compilation.
        /// </summary>
        public LanguageVersion CSharpLanguageVersion { get; set; } = LanguageVersion.CSharp10;

        /// <summary>
        /// Gets a flag that determines if localized component names should be supported.</c>.
        /// </summary>
        public bool SupportLocalizedComponentNames { get; set; } = false;

        public bool Equals(RazorSourceGenerationOptions other)
        {
            return
                RootNamespace == other.RootNamespace &&
                Configuration.Equals(other.Configuration) &&
                GenerateMetadataSourceChecksumAttributes == other.GenerateMetadataSourceChecksumAttributes &&
                CSharpLanguageVersion == other.CSharpLanguageVersion &&
                SupportLocalizedComponentNames == other.SupportLocalizedComponentNames;
        }

        public override bool Equals(object obj) => obj is RazorSourceGenerationOptions other && Equals(other);

        public override int GetHashCode() => Configuration.GetHashCode();
    }
}
