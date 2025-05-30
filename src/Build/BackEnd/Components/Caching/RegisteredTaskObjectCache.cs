// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    /// <summary>
    /// This is a cache of objects which are registered to be disposed of at a specified time.
    /// </summary>
    internal class RegisteredTaskObjectCache : RegisteredTaskObjectCacheBase, IBuildComponent, IRegisteredTaskObjectCache, IDisposable
    {
        /// <summary>
        /// Finalizes an instance of the <see cref="RegisteredTaskObjectCache"/> class.
        /// </summary>
        ~RegisteredTaskObjectCache()
        {
            Dispose(disposing: false);
        }

        #region IBuildComponent

        /// <summary>
        /// Initialize the build component.
        /// </summary>
        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        /// <summary>
        /// Shuts down the build component.
        /// </summary>
        public void ShutdownComponent()
        {
            ErrorUtilities.VerifyThrow(IsCollectionEmptyOrUncreated(RegisteredTaskObjectLifetime.Build), "Build lifetime objects were not disposed at the end of the build");
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Implementation of Dispose pattern.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        /// <summary>
        /// Component factory.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.RegisteredTaskObjectCache, "Cannot create components of type {0}", type);
            return new RegisteredTaskObjectCache();
        }

        /// <summary>
        /// Implementation of Dispose pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ErrorUtilities.VerifyThrow(IsCollectionEmptyOrUncreated(RegisteredTaskObjectLifetime.Build), "Build lifetime objects were not disposed at the end of the build");
            }
        }
    }
}
