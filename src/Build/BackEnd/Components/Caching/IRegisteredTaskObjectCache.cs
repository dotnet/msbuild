﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

#if BUILD_ENGINE
namespace Microsoft.Build.BackEnd.Components.Caching
#else
namespace Microsoft.Build.Shared
#endif
{
    /// <summary>
    /// Defines a cache for registered task objects.
    /// </summary>
    internal interface IRegisteredTaskObjectCache
    {
        /// <summary>
        /// Disposes of all of the objects with the specified lifetime.
        /// </summary>
        void DisposeCacheObjects(RegisteredTaskObjectLifetime lifetime);

        /// <summary>
        /// Registers a task object with the specified key and lifetime.
        /// </summary>
        void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection);

        /// <summary>
        /// Gets a previously registered task object.
        /// </summary>
        object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime);

        /// <summary>
        /// Unregisters a task object.
        /// </summary>
        object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime);
    }
}
