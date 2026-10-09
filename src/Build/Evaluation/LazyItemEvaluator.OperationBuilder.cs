// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private class OperationBuilder
    {
        // WORKAROUND: Unnecessary boxed allocation: https://github.com/dotnet/corefx/issues/24563
        private static readonly ImmutableDictionary<string, LazyItemList> s_emptyIgnoreCase = ImmutableDictionary.Create<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        public ProjectItemElement ItemElement { get; set; }
        public string ItemType { get; set; }
        public ItemSpec<P, I> ItemSpec { get; set; }

        public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; } = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            ImmutableDictionary.CreateBuilder<string, LazyItemList>() :
            s_emptyIgnoreCase.ToBuilder();

        public bool ConditionResult { get; set; }

        public OperationBuilder(ProjectItemElement itemElement, bool conditionResult)
        {
            ItemElement = itemElement;
            ItemType = itemElement.ItemType;
            ConditionResult = conditionResult;
        }
    }
}
