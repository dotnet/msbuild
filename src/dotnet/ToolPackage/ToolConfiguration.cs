// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolConfiguration
    {
        public ToolConfiguration(
            string commandName,
            string toolAssemblyEntryPoint)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ToolConfigurationException(CommonLocalizableStrings.ToolSettingsMissingCommandName);
            }

            if (string.IsNullOrWhiteSpace(toolAssemblyEntryPoint))
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsMissingEntryPoint,
                        commandName));
            }

            EnsureNoInvalidFilenameCharacters(commandName);

            CommandName = commandName;
            ToolAssemblyEntryPoint = toolAssemblyEntryPoint;
        }

        private void EnsureNoInvalidFilenameCharacters(string commandName)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            if (commandName.IndexOfAny(invalidCharacters) != -1)
            {
                throw new ToolConfigurationException(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsInvalidCommandName,
                        commandName,
                        string.Join(", ", invalidCharacters.Select(c => $"'{c}'"))));
            }
        }

        public string CommandName { get; }
        public string ToolAssemblyEntryPoint { get; }
    }
}
