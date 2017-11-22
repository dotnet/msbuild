// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ToolPackageObtainer
{
    internal class ToolConfiguration
    {
        public ToolConfiguration(
            string commandName,
            string toolAssemblyEntryPoint)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentNullException(nameof(commandName), "Cannot be null or whitespace");
            }

            EnsureNoInvalidFilenameCharacters(commandName, nameof(toolAssemblyEntryPoint));

            if (string.IsNullOrWhiteSpace(toolAssemblyEntryPoint))
            {
                throw new ArgumentNullException(nameof(toolAssemblyEntryPoint), "Cannot be null or whitespace");
            }

            CommandName = commandName;
            ToolAssemblyEntryPoint = toolAssemblyEntryPoint;
        }

        private void EnsureNoInvalidFilenameCharacters(string commandName, string nameOfParam)
        {
            // https://stackoverflow.com/questions/1976007/what-characters-are-forbidden-in-windows-and-linux-directory-names
            char[] invalidCharactors = {'/', '<', '>', ':', '"', '/', '\\', '|', '?', '*'};
            if (commandName.IndexOfAny(invalidCharactors) != -1)
            {
                throw new ArgumentException(
                    paramName: nameof(nameOfParam),
                    message: "Cannot contain following character " + new string(invalidCharactors));
            }
        }


        public string CommandName { get; }
        public string ToolAssemblyEntryPoint { get; }
    }
}
