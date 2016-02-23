// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileContentFile
    {
        public string Path { get; set; }

        public string OutputPath { get; set; }

        public string PPOutputPath { get; set; }

        public BuildAction BuildAction { get; set; }

        public string CodeLanguage { get; set; }

        public bool CopyToOutput { get; set; } = false;
    }
}