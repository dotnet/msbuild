// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class CommandSettings
    {
        public CommandSettings(string name, string runner, FilePath executable)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Executable = executable;
        }

        public string Name { get; private set; }

        public string Runner { get; private set; }

        public FilePath Executable { get; private set; }
    }
}
