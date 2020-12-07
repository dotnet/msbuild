// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;

#if BUILD_ENGINE
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Components.Caching
#else
namespace Microsoft.Build.Shared
#endif
{
    /// <summary>
    /// This is a cache of objects which are registered to be disposed of at a specified time.
    /// </summary>
    internal class RegisteredTaskObjectCacheBase
    {
        /// <summary>
        /// The cache for AppDomain lifetime objects.
        /// </summary>
        private static Lazy<ConcurrentDictionary<object, object>> s_appDomainLifetimeObjects = new Lazy<ConcurrentDictionary<object, object>>();

        /// <summary>
        /// The cache for Build lifetime objects.
        /// </summary>
        private Lazy<ConcurrentDictionary<object, object>> _buildLifetimeObjects = new Lazy<ConcurrentDictionary<object, object>>();

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Static constructor which registers a callback to dispose of AppDomain-lifetime cache objects.
        /// </summary>
        static RegisteredTaskObjectCacheBase()
        {
            AppDomain.CurrentDomain.DomainUnload += new EventHandler((sender, args) =>
            {
                DisposeObjects(s_appDomainLifetimeObjects);
            });
        }
#endif

        #region IRegisteredTaskObjectCache

        /// <summary> 
        /// Disposes of all of the cached objects registered with the specified lifetime.
        /// </summary>
        public void DisposeCacheObjects(RegisteredTaskObjectLifetime lifetime)
        {
            var lazyCollection = GetLazyCollectionForLifetime(lifetime);
            DisposeObjects(lazyCollection);
        }

        /// <summary>
        /// Registers a task object with the specified key and lifetime.
        /// </summary>
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            ConcurrentDictionary<object, object> dict = GetCollectionForLifetime(lifetime, dontCreate: false);

            dict?.TryAdd(key, obj);
        }

        /// <summary>
        /// Gets a previously registered task object.
        /// </summary>
        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            ConcurrentDictionary<object, object> dict = GetCollectionForLifetime(lifetime, dontCreate: true);
            object obj = null;
            dict?.TryGetValue(key, out obj);

            return obj;
        }

        /// <summary>
        /// Unregisters a previously registered task object.
        /// </summary>
        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            ConcurrentDictionary<object, object> dict = GetCollectionForLifetime(lifetime, dontCreate: true);
            object obj = null;
            dict?.TryRemove(key, out obj);

            return obj;
        }

        #endregion

        /// <summary>
        /// Returns true if a collection is not yet created or if it has no content.
        /// </summary>
        protected bool IsCollectionEmptyOrUncreated(RegisteredTaskObjectLifetime lifetime)
        {
            var collection = GetCollectionForLifetime(lifetime, dontCreate: true);
            return (collection == null) || (collection.Count == 0);
        }

        /// <summary>
        /// Returns the collection associated with a particular lifetime.
        /// </summary>
        protected ConcurrentDictionary<object, object> GetCollectionForLifetime(RegisteredTaskObjectLifetime lifetime, bool dontCreate)
        {
            Lazy<ConcurrentDictionary<object, object>> dict = GetLazyCollectionForLifetime(lifetime);

            // If we aren't supposed to create it, don't force the creation.
            if (dontCreate && !dict.IsValueCreated)
            {
                return null;
            }

            return dict.Value;
        }

        /// <summary>
        /// Gets the lazy cache for the specified lifetime.
        /// </summary>
        protected Lazy<ConcurrentDictionary<object, object>> GetLazyCollectionForLifetime(RegisteredTaskObjectLifetime lifetime)
        {
            Lazy<ConcurrentDictionary<object, object>> dict = null;
            switch (lifetime)
            {
                case RegisteredTaskObjectLifetime.Build:
                    dict = _buildLifetimeObjects;
                    break;

                case RegisteredTaskObjectLifetime.AppDomain:
                    dict = RegisteredTaskObjectCacheBase.s_appDomainLifetimeObjects;
                    break;
            }

            return dict;
        }

        /// <summary>
        /// Cleans up a cache collection.
        /// </summary>
        private static void DisposeObjects(Lazy<ConcurrentDictionary<object, object>> lifetimeObjects)
        {
            if (lifetimeObjects.IsValueCreated)
            {
                foreach (var obj in lifetimeObjects.Value.Values)
                {
                    try
                    {
                        IDisposable disposable = obj as IDisposable;
                        disposable?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionHandling.IsCriticalException(ex))
                        {
                            throw;
                        }

                        // Eat it.  We don't have a way to log here because at a minimum the build has already completed.
                    }
                }

                lifetimeObjects.Value.Clear();
            }
        }
    }
}
