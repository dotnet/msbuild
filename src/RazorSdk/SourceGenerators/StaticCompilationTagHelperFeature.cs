// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal sealed class StaticCompilationTagHelperFeature : RazorEngineFeatureBase, ITagHelperFeature
    {
        private static readonly List<TagHelperDescriptor> EmptyList = new();
        
        private ITagHelperDescriptorProvider[]? _providers;

        public List<TagHelperDescriptor> GetDescriptors()
        {
            if (Compilation is null)
            {
                return EmptyList;
            }

            var results = new List<TagHelperDescriptor>();
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(Compilation);
            context.Items.SetTargetAssembly(TargetAssembly!);

            for (var i = 0; i < _providers?.Length; i++)
            {
                _providers[i].Execute(context);
            }

            return results;
        }

        IReadOnlyList<TagHelperDescriptor> ITagHelperFeature.GetDescriptors() => GetDescriptors();

        public Compilation? Compilation { get; set; }

        public IAssemblySymbol? TargetAssembly { get; set; }

        protected override void OnInitialized()
        {
            _providers = Engine.Features.OfType<ITagHelperDescriptorProvider>().OrderBy(f => f.Order).ToArray();
        }
    }
}
