// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal sealed class StaticTagHelperFeature : RazorEngineFeatureBase, ITagHelperFeature
    {
        public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; set; }

        public IReadOnlyList<TagHelperDescriptor> GetDescriptors() => TagHelpers;

        public StaticTagHelperFeature()
        {
            TagHelpers = new List<TagHelperDescriptor>();
        }

        public StaticTagHelperFeature(IEnumerable<TagHelperDescriptor> tagHelpers)
        {
            TagHelpers = new List<TagHelperDescriptor>(tagHelpers);
        }
    }
}
