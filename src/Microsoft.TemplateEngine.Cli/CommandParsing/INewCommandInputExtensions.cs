// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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

        internal static string SearchCommandExample(this INewCommandInput command, string templateName)
        {
            if (templateName.Any(char.IsWhiteSpace))
            {
                templateName = $"'{templateName}'";
            }

            return $"dotnet {command.CommandName} {templateName} --search";
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
    }
}
