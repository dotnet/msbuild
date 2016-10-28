// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal.PathSegments
{
    internal class RecursiveWildcardSegment : IPathSegment
    {
        public bool CanProduceStem { get { return true; } }

        public bool Match(string value)
        {
            return false;
        }
    }
}