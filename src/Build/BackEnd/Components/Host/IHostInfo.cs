// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd.Components.Host
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// The default registration is <see cref="TransientHostInfo"/>; specific hosts
    /// (currently the MSBuild Server) replace the registration with
    /// <see cref="LongLivedServerHostInfo"/> at startup.
    /// </remarks>
    internal interface IHostInfo : IBuildComponent
    {
        /// <summary>
        /// Returns true when the build engine is hosted in a process whose
        /// lifetime extends beyond a single build invocation (MSBuild Server).
        /// </summary>
        bool IsLongLivedHost { get; }
    }

    /// <summary>
    /// Default <see cref="IHostInfo"/> implementation, used when the engine
    /// is hosted in a regular short-lived process (one build per process).
    /// </summary>
    internal sealed class TransientHostInfo : IHostInfo
    {
        public bool IsLongLivedHost => false;

        public void InitializeComponent(IBuildComponentHost host) { }

        public void ShutdownComponent() { }

        internal static IBuildComponent CreateComponent(BuildComponentType type)
            => new TransientHostInfo();
    }

    /// <summary>
    /// <see cref="IHostInfo"/> implementation registered by the MSBuild Server
    /// to mark the engine as running in a long-lived host. This is the workaround
    /// path for https://github.com/dotnet/msbuild/issues/13315.
    /// </summary>
    internal sealed class LongLivedServerHostInfo : IHostInfo
    {
        public bool IsLongLivedHost => true;

        public void InitializeComponent(IBuildComponentHost host) { }

        public void ShutdownComponent() { }

        internal static IBuildComponent CreateComponent(BuildComponentType type)
            => new LongLivedServerHostInfo();
    }
}
