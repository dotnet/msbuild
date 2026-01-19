// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Default implementation of <see cref="ITaskEnvironmentDriver"/> that directly interacts with the file system
    /// and environment variables. Used in multi-process mode of execution.
    /// </summary>
    /// <remarks>
    /// Implemented as a singleton since it has no instance state.
    /// </remarks>
    internal sealed class MultiProcessTaskEnvironmentDriver : ITaskEnvironmentDriver
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        private static readonly MultiProcessTaskEnvironmentDriver s_instance = new MultiProcessTaskEnvironmentDriver();

        /// <summary>
        /// Gets the singleton instance of <see cref="MultiProcessTaskEnvironmentDriver"/>.
        /// </summary>
        public static MultiProcessTaskEnvironmentDriver Instance => s_instance;

        private MultiProcessTaskEnvironmentDriver() { }

        /// <inheritdoc/>
        public AbsolutePath ProjectDirectory
        {
            get => new AbsolutePath(NativeMethodsShared.GetCurrentDirectory(), ignoreRootedCheck: true);
            set => NativeMethodsShared.SetCurrentDirectory(value.Value);
        }

        /// <inheritdoc/>
        public AbsolutePath GetAbsolutePath(string path)
        {
            // This function should not throw when path has illegal characters.
            // For .NET Framework, Microsoft.IO.Path.Combine should be used instead of System.IO.Path.Combine to achieve it.
            // For .NET Core, System.IO.Path.Combine already does not throw in this case.
            return new AbsolutePath(Path.Combine(NativeMethodsShared.GetCurrentDirectory(), path), ignoreRootedCheck: true);
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
            CommunicationsUtilities.SetEnvironmentVariable(name, value);
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
