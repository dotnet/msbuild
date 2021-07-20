
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class RazorSourceGenerationOptions : IEquatable<RazorSourceGenerationOptions>
    {
        public string RootNamespace { get; set; } = "ASP";

        public RazorConfiguration Configuration { get; set; } = RazorConfiguration.Default;

        /// <summary>
        /// Gets a flag that determines if the source generator waits for the debugger to attach.
        /// <para>
        /// To configure this using MSBuild, use the <c>_RazorSourceGeneratorDebug</c> property.
        /// For instance <c>dotnet msbuild /p:_RazorSourceGeneratorDebug=true</c>
        /// </para>
        /// </summary>
        public bool WaitForDebugger { get; set; } = false;

        /// <summary>
        /// Gets a flag that determines if generated Razor views and Pages includes the <c>RazorSourceChecksumAttribute</c>.
        /// </summary>
        public bool GenerateMetadataSourceChecksumAttributes { get; set; } = false;

        /// <summary>
        /// Gets a flag that determines if the source generator should no-op.
        /// <para>
        /// This flag exists to support scenarios in VS where design-time and EnC builds need
        /// to run without invoking the source generator to avoid duplicate types being produced.
        /// The property is set by the SDK via an editor config.
        /// </para>
        /// </summary>
        public bool SuppressRazorSourceGenerator { get; set; } = false;

        /// <summary>
        /// Gets the CSharp language version currently used by the compilation.
        /// </summary>
        public LanguageVersion CSharpLanguageVersion { get; set; } = LanguageVersion.CSharp10;

        public bool Equals(RazorSourceGenerationOptions other)
        {
            return RootNamespace == other.RootNamespace &&
                Configuration == other.Configuration &&
                GenerateMetadataSourceChecksumAttributes == other.GenerateMetadataSourceChecksumAttributes &&
                SuppressRazorSourceGenerator == other.SuppressRazorSourceGenerator &&
                CSharpLanguageVersion == other.CSharpLanguageVersion;
        }

        public override bool Equals(object obj) => obj is RazorSourceGenerationOptions other && Equals(other);

        public override int GetHashCode() => Configuration.GetHashCode();
    }
}
