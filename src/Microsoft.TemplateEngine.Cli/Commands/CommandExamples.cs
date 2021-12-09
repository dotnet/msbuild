// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Cli.Commands
{
    //TODO: consider refactoring based on command definition

    /// <summary>
    /// Use these extensions to get examples of dotnet new commands.
    /// </summary>
    internal static class CommandExamples
    {
        internal static string InstallCommandExample(string commandName, bool withVersion = false,  string packageID = "", string version = "")
        {
            if (string.IsNullOrWhiteSpace(packageID))
            {
                return withVersion
                    ? $"dotnet {commandName} --install <PACKAGE_ID>::<VERSION>"
                    : $"dotnet {commandName} --install <PACKAGE_ID>";
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                if (packageID.Any(char.IsWhiteSpace))
                {
                    packageID = $"'{packageID}'";
                }
                return $"dotnet {commandName} --install {packageID}";
            }
            else
            {
                string packageAndVersion = $"{packageID}::{version}";
                if (packageAndVersion.Any(char.IsWhiteSpace))
                {
                    packageAndVersion = $"'{packageAndVersion}'";
                }
                return $"dotnet {commandName} --install {packageAndVersion}";
            }
        }

        internal static string UpdateApplyCommandExample(string commandName)
        {
            return $"dotnet {commandName} --update-apply";
        }

        internal static string ListCommandExample(string commandName)
        {
            return $"dotnet {commandName} --list";
        }

        internal static string SearchCommandExample(string commandName, string? templateName = null, IEnumerable<string>? additionalArgs = null, bool usePlaceholder = false)
        {
            if (usePlaceholder)
            {
                templateName = "<TEMPLATE_NAME>";
            }
            if (string.IsNullOrWhiteSpace(templateName) && (additionalArgs == null || !additionalArgs.Any()))
            {
                throw new ArgumentException($"{nameof(templateName)} should not be empty when {nameof(usePlaceholder)} is false and no additional arguments is given.", nameof(templateName));
            }
            string commandStr = $"dotnet {commandName}";
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                if (templateName?.Any(char.IsWhiteSpace) ?? false)
                {
                    templateName = $"'{templateName}'";
                }
                commandStr += $" {templateName}";
            }

            commandStr += $" --search";
            if (additionalArgs?.Any() ?? false)
            {
                commandStr += $" {string.Join(" ", additionalArgs)}";
            }
            return commandStr;
        }

        internal static string UninstallCommandExample(string commandName, string packageId = "", bool noArgs = false)
        {
            if (noArgs)
            {
                return $"dotnet {commandName} --uninstall";
            }

            if (string.IsNullOrWhiteSpace(packageId))
            {
                return $"dotnet {commandName} --uninstall <PACKAGE_ID>";
            }

            if (packageId.Any(char.IsWhiteSpace))
            {
                packageId = $"'{packageId}'";
            }
            return $"dotnet {commandName} --uninstall {packageId}";
        }

        internal static string InstantiateTemplateExample (string commandName, string templateName)
        {
            if (templateName.Any(char.IsWhiteSpace))
            {
                templateName = $"'{templateName}'";
            }
            return $"dotnet {commandName} {templateName}";
        }

        internal static string HelpCommandExample(string commandName, string? templateName = null, string? language = null, string? type = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return $"dotnet {commandName} -h";
            }
            if (templateName.Any(char.IsWhiteSpace))
            {
                templateName = $"'{templateName}'";
            }
            string commandStr = $"dotnet {commandName} {templateName} -h";
            if (!string.IsNullOrWhiteSpace(language))
            {
                commandStr += $" --language {language}";
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                commandStr += $" --type {type}";
            }
            return commandStr;
        }

        internal static string New3CommandExample(string commandName)
        {
            return $"dotnet {commandName}";
        }
    }
}
