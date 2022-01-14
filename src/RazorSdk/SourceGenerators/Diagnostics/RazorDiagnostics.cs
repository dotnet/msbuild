// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.NET.Sdk.Razor.SourceGenerators.Diagnostics;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal static class RazorDiagnostics
    {
        public static readonly DiagnosticDescriptor InvalidRazorLangVersionDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.InvalidRazorLangVersionRuleId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.InvalidRazorLangTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.InvalidRazorLangMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ReComputingTagHelpersDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.ReComputingTagHelpersRuleId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.RecomputingTagHelpersTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.RecomputingTagHelpersMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TargetPathNotProvided = new DiagnosticDescriptor(
            DiagnosticIds.TargetPathNotProvidedRuleId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.TargetPathNotProvidedTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.TargetPathNotProvidedMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor GeneratedOutputFullPathNotProvided = new DiagnosticDescriptor(
            DiagnosticIds.GeneratedOutputFullPathNotProvidedRuleId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.GeneratedOutputFullPathNotProvidedTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.GeneratedOutputFullPathNotProvidedMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor CurrentCompilationReferenceNotFoundDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.CurrentCompilationReferenceNotFoundId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.CurrentCompilationReferenceNotFoundTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.CurrentCompilationReferenceNotFoundMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor SkippingGeneratedFileWriteDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.SkippingGeneratedFileWriteId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.SkippingGeneratedFileWriteTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.SkippingGeneratedFileWriteMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor SourceTextNotFoundDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.SourceTextNotFoundId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.SourceTextNotFoundTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.SourceTextNotFoundMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnexpectedProjectItemReadCallDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.UnexpectedProjectItemReadCallId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.UnexpectedProjectItemReadCallTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.UnexpectedProjectItemReadCallMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor InvalidRazorContextComputedDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.InvalidRazorContextComputedId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.InvalidRazorContextComputedTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.InvalidRazorContextComputedMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor MetadataReferenceNotProvidedDescriptor = new DiagnosticDescriptor(
            DiagnosticIds.MetadataReferenceNotProvidedId,
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.MetadataReferenceNotProvidedTitle), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            new LocalizableResourceString(nameof(RazorSourceGeneratorResources.MetadataReferenceNotProvidedMessage), RazorSourceGeneratorResources.ResourceManager, typeof(RazorSourceGeneratorResources)),
            "RazorSourceGenerator",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true
        );

        public static Diagnostic AsDiagnostic(this RazorDiagnostic razorDiagnostic)
        {
            var descriptor = new DiagnosticDescriptor(
                razorDiagnostic.Id,
                razorDiagnostic.GetMessage(CultureInfo.CurrentCulture),
                razorDiagnostic.GetMessage(CultureInfo.CurrentCulture),
                "Razor",
                razorDiagnostic.Severity switch
                {
                    RazorDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                    RazorDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                    _ => DiagnosticSeverity.Hidden,
                },
                isEnabledByDefault: true);

            var span = razorDiagnostic.Span;

            Location location;
            if (span == SourceSpan.Undefined)
            {
                // TextSpan.Empty
                location = Location.None;
            }
            else
            {
                var linePosition = new LinePositionSpan(
                    new LinePosition(span.LineIndex, span.CharacterIndex),
                    new LinePosition(span.LineIndex, span.CharacterIndex + span.Length));

                location = Location.Create(
                   span.FilePath,
                   span.AsTextSpan(),
                   linePosition);
            }

            return Diagnostic.Create(descriptor, location);
        }
    }
}
