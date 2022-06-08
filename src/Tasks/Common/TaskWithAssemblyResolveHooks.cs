// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Inherit from this task instead of <see cref="TaskBase"/> if your task needs "binding redirects"
    /// to the latest versions of assemblies that ship as part of the SDK.
    /// </summary>
    public abstract class TaskWithAssemblyResolveHooks : TaskBase
    {
#if NET472
        /// <summary>
        /// AssemblyResolve event handler that will bind to the System.Reflection.Metadata that ships with the SDK.
        /// </summary>
        /// <remarks>
        /// This should not be necessary, but the current version of Microsoft.NET.HostModel predates .NET 5.0
        /// and thus has a dependency on System.Reflection.Metadata 1.4.5.0 and System.Collections.Immutable 1.2.5.0,
        /// while the SDK ships with 5.0.0.0 versions. This will fail to resolve SRM/SCI at runtime. We can add this
        /// hook to load whatever version is shipping with the SDK, which should always be higher than the HostModel
        /// reference.
        /// 
        /// DELETE THIS when/if HostModel is updated to have an SRM dependency that's coherent with .NET.
        /// </remarks>
        private static Assembly ResolverForBindingRedirects(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new(args.Name);
            return name.Name switch
            {
                "System.Reflection.Metadata" or "System.Collections.Immutable" =>
                    Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{name.Name}.dll")),
                _ => null,
            };
        }

        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolverForBindingRedirects;

            try
            {
                return base.Execute();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolverForBindingRedirects;
            }
        }
#endif
    }
}
