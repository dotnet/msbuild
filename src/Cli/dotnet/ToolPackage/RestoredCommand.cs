// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
