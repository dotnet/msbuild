// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;

#nullable disable

#if BUILD_ENGINE
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
        private static readonly Lazy<ConcurrentDictionary<object, object>> s_appDomainLifetimeObjects = new Lazy<ConcurrentDictionary<object, object>>();

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
        /// <param name="key">The key used to register the object.</param>
        /// <param name="obj">The object to register.</param>
        /// <param name="lifetime">The lifetime for which the object should be retained.</param>
        /// <param name="allowEarlyCollection">
        /// This parameter is currently ignored. It was intended to allow the registered object to be collected
        /// before the end of its requested lifetime, but that behavior was dropped in a pre-open-source release.
        /// Registered objects are always retained for the full requested <paramref name="lifetime"/> regardless of
        /// the value passed here.
        /// </param>
        /// <remarks>
        /// The <paramref name="allowEarlyCollection"/> argument has no effect. It is retained on the signature for
        /// backwards compatibility with callers, but objects are never collected early — they live for the entire
        /// requested lifetime. This has been the behavior since before MSBuild was open sourced.
        /// </remarks>
        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            // Note: allowEarlyCollection is intentionally unused; see the remarks above.
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
            return (collection == null) || collection.IsEmpty;
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
                    catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                    {
                        // Eat it.  We don't have a way to log here because at a minimum the build has already completed.
                    }
                }

                lifetimeObjects.Value.Clear();
            }
        }
    }
}
