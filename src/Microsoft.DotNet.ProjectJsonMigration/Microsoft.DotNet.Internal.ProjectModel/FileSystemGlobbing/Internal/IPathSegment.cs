// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal
{
    internal interface IPathSegment
    {
        bool CanProduceStem { get; }

        bool Match(string value);
    }
}