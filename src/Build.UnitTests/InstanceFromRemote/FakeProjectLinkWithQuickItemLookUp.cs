// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// A fake implementation of ProjectLink that supports quick lookup of items by evaluated include.
    /// </summary>
    internal sealed class FakeProjectLinkWithQuickItemLookUp : FakeProjectLink
    {
        private readonly IDictionary<string, ProjectItemLink[]> _itemsByEvaluatedInclude;

        public FakeProjectLinkWithQuickItemLookUp(
            string path,
            IDictionary<string, ProjectItemLink[]> itemsByEvaluatedInclude,
            IDictionary<string, ProjectItemDefinition>? itemDefinitions = null)
            : base(path, itemDefinitions: itemDefinitions)
        {
            _itemsByEvaluatedInclude = itemsByEvaluatedInclude ?? throw new ArgumentNullException(nameof(itemsByEvaluatedInclude));
        }

        public override ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude)
        {
            if (_itemsByEvaluatedInclude.TryGetValue(evaluatedInclude, out ProjectItemLink[]? items))
            {
                var factory = LinkedObjectsFactory.Get(ProjectCollection.GlobalProjectCollection);
                return items.Select(link => factory.Create(link, null, null)).ToList();
            }

            return Array.Empty<ProjectItem>();
        }
    }
}
