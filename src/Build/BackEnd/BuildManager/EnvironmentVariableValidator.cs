// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Constants = Microsoft.Build.Framework.Constants;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Validates environment variables at build startup and logs warnings for misconfiguration.
    /// This class centralizes environment variable validation logic to avoid bloating BuildManager.
    /// </summary>
    internal static class EnvironmentVariableValidator
    {
        /// <summary>
        /// Runs all environment variable validations and logs appropriate warnings.
        /// Called once at the start of a build session from BuildManager.BeginBuild.
        /// </summary>
        /// <param name="loggingService">The logging service to use for logging warnings.</param>
        internal static void ValidateEnvironmentVariables(ILoggingService loggingService)
        {
            ValidateDotnetHostPath(loggingService);
            // Future environment variable validations can be added here
        }

        /// <summary>
        /// Validates the DOTNET_HOST_PATH environment variable and logs a warning if it points to a directory instead of a file.
        /// </summary>
        private static void ValidateDotnetHostPath(ILoggingService loggingService)
        {
            string? dotnetHostPath = Environment.GetEnvironmentVariable(Constants.DotnetHostPathEnvVarName);
            if (string.IsNullOrEmpty(dotnetHostPath))
            {
                return;
            }

            try
            {
                if (FileSystems.Default.DirectoryExists(dotnetHostPath))
                {
                    loggingService.LogWarning(BuildEventContext.Invalid, null, BuildEventFileInfo.Empty, "DotnetHostPathIsDirectory", dotnetHostPath);
                }
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Silently ignore I/O exceptions when checking the path - this validation is best-effort
                // and should not cause build failures if the path cannot be checked.
            }
        }
    }
}
