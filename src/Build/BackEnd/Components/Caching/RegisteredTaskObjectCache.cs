// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Components.Caching
{
    /// <summary>
    /// This is a cache of objects which are registered to be disposed of at a specified time.
    /// </summary>
    internal class RegisteredTaskObjectCache : RegisteredTaskObjectCacheBase, IBuildComponent, IRegisteredTaskObjectCache, IDisposable
    {
        /// <summary>
        /// Finalizer
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
