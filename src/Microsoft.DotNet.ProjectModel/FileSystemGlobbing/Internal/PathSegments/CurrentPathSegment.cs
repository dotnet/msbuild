// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PathSegments
{
    public class CurrentPathSegment : IPathSegment
    {
        public bool CanProduceStem { get { return false; } }

        public bool Match(string value)
        {
            return false;
        }
    }
}