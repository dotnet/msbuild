// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Tasks
{
    internal sealed class FallbackMultiProcessEnvironmentDriver : ITaskEnvironmentDriver
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        private static readonly FallbackMultiProcessEnvironmentDriver s_instance = new FallbackMultiProcessEnvironmentDriver();

        /// <summary>
        /// Gets the singleton instance of <see cref="FallbackMultiProcessEnvironmentDriver"/>.
        /// </summary>
        public static FallbackMultiProcessEnvironmentDriver Instance => s_instance;

        private FallbackMultiProcessEnvironmentDriver() { }

        /// <inheritdoc/>
        public AbsolutePath ProjectDirectory
        {
            get => new AbsolutePath(NativeMethodsShared.GetCurrentDirectory(), ignoreRootedCheck: true);
            set => NativeMethodsShared.SetCurrentDirectory(value.Value);
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            return new AbsolutePath(path, ProjectDirectory);
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return CommunicationsUtilities.GetEnvironmentVariables();
        }

        /// <inheritdoc/>
        public void SetEnvironmentVariable(string name, string? value)
        {
#if !NETSTANDARD
            CommunicationsUtilities.SetEnvironmentVariable(name, value);
#else
            Environment.SetEnvironmentVariable(name, value);
#endif
        }

        /// <inheritdoc/>
        public void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            CommunicationsUtilities.SetEnvironment(newEnvironment);
        }

        /// <inheritdoc/>
        public ProcessStartInfo GetProcessStartInfo()
        {
            return new ProcessStartInfo();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Singleton instance, no cleanup needed.
        }
    }
}
