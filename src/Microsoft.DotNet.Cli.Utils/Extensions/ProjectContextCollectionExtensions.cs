// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ProjectContextCollectionExtensions
    {
        public static ProjectContextCollection EnsureValid(this ProjectContextCollection contextCollection, string projectFilePath)
        {
            IEnumerable<DiagnosticMessage> errors;

            if (contextCollection == null)
            {
                errors = new[]
                {
                    new DiagnosticMessage(
                        ErrorCodes.DOTNET1017,
                        $"Project file does not exist '{ProjectPathHelper.NormalizeProjectFilePath(projectFilePath)}'.",
                        projectFilePath,
                        DiagnosticMessageSeverity.Error)
                };
            }
            else
            {
                errors = contextCollection
                            .ProjectDiagnostics
                            .Where(d => d.Severity == DiagnosticMessageSeverity.Error);
            }

            if (errors.Any())
            {
                StringBuilder errorMessage = new StringBuilder($"The current project is not valid because of the following errors:{Environment.NewLine}");

                foreach (DiagnosticMessage message in errors)
                {
                    errorMessage.AppendLine(message.FormattedMessage);
                }

                throw new GracefulException(errorMessage.ToString());
            }
            
            return contextCollection;
        }
    }
}
