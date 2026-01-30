// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Reflection;

#nullable disable

namespace AssemblyLoadContextTest
{
    /// <summary>
    /// Task that validates assembly version roll-forward behavior.
    /// Tests that MSBuildLoadContext accepts newer assembly versions when older versions are requested.
    /// </summary>
    public class ValidateAssemblyVersionRollForward : Task
    {
        /// <summary>
        /// The name of the assembly to check (e.g., "System.Collections.Immutable")
        /// </summary>
        [Required]
        public string AssemblyName { get; set; }

        /// <summary>
        /// The minimum expected version (e.g., "1.0.0.0")
        /// </summary>
        [Required]
        public string MinimumVersion { get; set; }

        public override bool Execute()
        {
            try
            {
                // Try to load the assembly by name with minimum version
                var minimumVersion = Version.Parse(MinimumVersion);
                var assemblyName = new AssemblyName(AssemblyName)
                {
                    Version = minimumVersion
                };

                // This will trigger MSBuildLoadContext.Load which should accept newer versions
                var assembly = Assembly.Load(assemblyName);
                var loadedVersion = assembly.GetName().Version;

                Log.LogMessage(MessageImportance.High,
                    $"Requested {AssemblyName} version {minimumVersion}, loaded version {loadedVersion}");

                // Verify that we got a version >= minimum
                if (loadedVersion < minimumVersion)
                {
                    Log.LogError(
                        $"Assembly version roll-forward failed: requested {minimumVersion}, but loaded {loadedVersion} which is older");
                    return false;
                }

                Log.LogMessage(MessageImportance.High,
                    $"Assembly version roll-forward succeeded: loaded version {loadedVersion} >= requested {minimumVersion}");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }
    }

    public class RegisterObject : Task
    {
        internal const string CacheKey = "RegressionForMSBuild#5080";

        public override bool Execute()
        {
            BuildEngine4.RegisterTaskObject(
                  CacheKey,
                  new RegisterObject(),
                  RegisteredTaskObjectLifetime.Build,
                  allowEarlyCollection: false);

            return true;
        }
    }

    public class RetrieveObject : Task
    {
        public override bool Execute()
        {
            var entry = (RegisterObject)BuildEngine4.GetRegisteredTaskObject(RegisterObject.CacheKey, RegisteredTaskObjectLifetime.Build);

            return true;
        }
    }
}
