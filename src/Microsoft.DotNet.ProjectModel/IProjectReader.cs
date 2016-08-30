// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    public interface IProjectReader
    {
        Project ReadProject(string projectPath, ProjectReaderSettings settings = null);
    }
}