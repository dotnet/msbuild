// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal interface ITransformApplicator
    {
        void Execute<T, U>(
            T element,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer;

        void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer;
    }
}