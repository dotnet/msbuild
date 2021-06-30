// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    /// <summary>
    /// Use these extensions to get examples of dotnet new commands.
    /// </summary>
    internal static class INewCommandInputExtensions
    {
        internal static string InstallCommandExample(this INewCommandInput command, bool withVersion = false,  string packageID = "", string version = "")
        {
            if (string.IsNullOrWhiteSpace(packageID))
            {
                return withVersion
                    ? $"dotnet {command.CommandName} --install <PACKAGE_ID>::<VERSION>"
                    : $"dotnet {command.CommandName} --install <PACKAGE_ID>";
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                return $"dotnet {command.CommandName} --install {packageID}";
            }
            else
            {
                return $"dotnet {command.CommandName} --install {packageID}::{version}";
            }
        }

        internal static string UpdateApplyCommandExample(this INewCommandInput command)
        {
            return $"dotnet {command.CommandName} --update-apply";
        }

        internal static string ListCommandExample(this INewCommandInput command)
        {
            return $"dotnet {command.CommandName} --list";
        }

        internal static string SearchCommandExample(this INewCommandInput command, string? templateName = null, IEnumerable<string>? additionalArgs = null, bool usePlaceholder = false)
        {
            if (usePlaceholder)
            {
                templateName = "<TEMPLATE_NAME>";
            }
            if (string.IsNullOrWhiteSpace(templateName) && (additionalArgs == null || !additionalArgs.Any()))
            {
                throw new ArgumentException($"{nameof(templateName)} should not be empty when {nameof(usePlaceholder)} is false and no additional arguments is given.", nameof(templateName));
            }
            string commandStr = $"dotnet {command.CommandName}";
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

        internal static string UninstallCommandExample(this INewCommandInput command, string packageId = "", bool noArgs = false)
        {
            if (noArgs)
            {
                return $"dotnet {command.CommandName} --uninstall";
            }

            if (string.IsNullOrWhiteSpace(packageId))
            {
                return $"dotnet {command.CommandName} --uninstall <PACKAGE_ID>";
            }
            return $"dotnet {command.CommandName} --uninstall {packageId}";
        }

        internal static string InstantiateTemplateExample (this INewCommandInput command, string templateName)
        {
            if (templateName.Any(char.IsWhiteSpace))
            {
                templateName = $"'{templateName}'";
            }
            return $"dotnet {command.CommandName} {templateName}";
        }

        internal static string HelpCommandExample(this INewCommandInput command, string? templateName = null, string? language = null)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return $"dotnet {command.CommandName} -h";
            }
            if (templateName.Any(char.IsWhiteSpace))
            {
                templateName = $"'{templateName}'";
            }
            string commandStr = $"dotnet {command.CommandName} {templateName} -h";
            if (!string.IsNullOrWhiteSpace(language))
            {
                commandStr += $" --language {language}";
            }
            return commandStr;
        }

        internal static string New3CommandExample(this INewCommandInput command)
        {
            return $"dotnet {command.CommandName}";
        }
    }
}
