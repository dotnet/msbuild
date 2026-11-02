// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

internal static class DiagnosticSubcategoryExtensions
{
    public static string? GetDisplayString(this DiagnosticSubcategory subcategory)
        => subcategory switch
        {
            DiagnosticSubcategory.SchemaValidation => SR.SubCategoryForSchemaValidationErrors,
            DiagnosticSubcategory.SolutionFile => SR.SubCategoryForSolutionParsingErrors,
            DiagnosticSubcategory.None => null,

            _ => Assumed.Unreachable<string?>($"Unexpected {nameof(DiagnosticSubcategory)} value: {subcategory}"),
        };
}
