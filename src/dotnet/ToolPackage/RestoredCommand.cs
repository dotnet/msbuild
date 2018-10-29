// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class RestoredCommand
    {
        public RestoredCommand(
            ToolCommandName name,
            string runner,
            FilePath executable)
        {
            Name = name;
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Executable = executable;
        }

        public ToolCommandName Name { get; private set; }

        public string Runner { get; private set; }

        public FilePath Executable { get; private set; }

        public string DebugToString()
        {
            return $"ToolCommandName: {Name.Value} - Runner: {Runner} - FilePath: {Executable.Value}";
        }
    }
}
