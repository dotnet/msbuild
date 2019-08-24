// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class ItemFactoryWrapper : IItemFactory<I, I>
        {
            ProjectItemElement _itemElement;
            IItemFactory<I, I> _wrappedItemFactory;

            public ItemFactoryWrapper(ProjectItemElement itemElement, IItemFactory<I, I> wrappedItemFactory)
            {
                _itemElement = itemElement;
                _wrappedItemFactory = wrappedItemFactory;
            }

            void SetItemElement()
            {
                _wrappedItemFactory.ItemElement = _itemElement;
            }

            public ProjectItemElement ItemElement
            {
                set
                {
                    _itemElement = value;
                    SetItemElement();
                }
            }

            public string ItemType
            {
                get
                {
                    SetItemElement();
                    return _wrappedItemFactory.ItemType;
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public I CreateItem(I source, string definingProject)
            {
                SetItemElement();
                return _wrappedItemFactory.CreateItem(source, definingProject);
            }

            public I CreateItem(string include, string definingProject)
            {
                SetItemElement();
                return _wrappedItemFactory.CreateItem(include, definingProject);
            }

            public I CreateItem(string include, string includeBeforeWildcardExpansion, string definingProject)
            {
                SetItemElement();
                return _wrappedItemFactory.CreateItem(include, includeBeforeWildcardExpansion, definingProject);
            }

            public I CreateItem(string include, I baseItem, string definingProject)
            {
                SetItemElement();
                return _wrappedItemFactory.CreateItem(include, baseItem, definingProject);
            }

            public void SetMetadata(IEnumerable<Pair<ProjectMetadataElement, string>> metadata, IEnumerable<I> destinationItems)
            {
                SetItemElement();
                _wrappedItemFactory.SetMetadata(metadata, destinationItems);
            }
        }
    }
}
