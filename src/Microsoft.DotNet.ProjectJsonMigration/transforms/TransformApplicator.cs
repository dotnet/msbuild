// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class TransformApplicator : ITransformApplicator
    {
        private readonly ITransformApplicator _propertyTransformApplicator;

        private readonly ITransformApplicator _itemTransformApplicator;

        public TransformApplicator(ITransformApplicator propertyTransformApplicator=null, ITransformApplicator itemTransformApplicator=null)
        {
            _propertyTransformApplicator = propertyTransformApplicator ?? new PropertyTransformApplicator();
            _itemTransformApplicator = propertyTransformApplicator ?? new ItemTransformApplicator();
        }

        public void Execute<T, U>(
            T element,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            if (typeof(T) == typeof(ProjectItemElement))
            {
                _itemTransformApplicator.Execute(element, destinationElement, mergeExisting);
            }
            else if (typeof(T) == typeof(ProjectPropertyElement))
            {
                _propertyTransformApplicator.Execute(element, destinationElement, mergeExisting);
            }
            else
            {
                throw new ArgumentException($"Unexpected type {nameof(T)}");
            }
        }

        public void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            foreach (var element in elements)
            {
                Execute(element, destinationElement, mergeExisting);
            }
        }
    }
}
