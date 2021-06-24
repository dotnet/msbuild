// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public partial class RazorSourceGenerator
    {
        private IReadOnlyList<TagHelperDescriptor> GetTagHelpers(IEnumerable<MetadataReference> references, StaticCompilationTagHelperFeature tagHelperFeature, Compilation compilation)
        {
            List<TagHelperDescriptor> descriptors = new();
            tagHelperFeature.Compilation = compilation;
            foreach (var reference in references)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    tagHelperFeature.TargetAssembly = assembly;
                    descriptors.AddRange(tagHelperFeature.GetDescriptors());
                }
            }
            return descriptors;
        }

        private IReadOnlyList<TagHelperDescriptor> GetTagHelpersFromCompilation(Compilation compilation, StaticCompilationTagHelperFeature tagHelperFeature, SyntaxTree syntaxTrees)
        {
            var compilationWithDeclarations = compilation.AddSyntaxTrees(syntaxTrees);

            tagHelperFeature.Compilation = compilationWithDeclarations;
            tagHelperFeature.TargetAssembly = compilationWithDeclarations.Assembly;

            return tagHelperFeature.GetDescriptors();
        }
    }
}