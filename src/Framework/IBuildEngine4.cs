// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Defines the lifetime of a registered task object.
    /// </summary>
    public enum RegisteredTaskObjectLifetime
    {
        /// <summary>
        /// The registered object will be disposed when the build ends.
        /// </summary>
        Build,

        /// <summary>
        /// The registered object will be disposed when the AppDomain is unloaded.
        /// </summary>
        /// <remarks>
        /// The AppDomain to which this refers is the one in which MSBuild was launched,
        /// not the one in which the Task was launched.
        /// </remarks>
        AppDomain,
    }

    /// <summary>
    /// This interface extends IBuildEngine to provide a mechanism allowing tasks to 
    /// share data between task invocations.
    /// </summary>
    public interface IBuildEngine4 : IBuildEngine3
    {
        /// <summary>
        /// Registers an object with the system that will be disposed of at some specified time
        /// in the future.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="obj">The object to be held for later disposal.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <param name="allowEarlyCollection">The object may be disposed earlier that the requested time if
        /// MSBuild needs to reclaim memory.</param>
        /// <remarks>
        /// <para>
        /// This method may be called by tasks which need to maintain state across task invocations,
        /// such as to cache data which may be expensive to generate but which is known not to change during the 
        /// build.  It is strongly recommended that <paramref name="allowEarlyCollection"/> be set to true if the
        /// object will retain any significant amount of data, as this gives MSBuild the most flexibility to 
        /// manage limited process memory resources.
        /// </para>
        /// <para>
        /// The thread on which the object is disposed may be arbitrary - however it is guaranteed not to
        /// be disposed while the task is executing, even if <paramref name="allowEarlyCollection"/> is set
        /// to true.
        /// </para>
        /// <para>
        /// If the object implements IDisposable, IDisposable.Dispose will be invoked on the object before
        /// discarding it.
        /// </para>
        /// </remarks>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "Shipped this way in Dev11 Beta, which is go-live")]
        void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection);

        /// <summary>
        /// Retrieves a previously registered task object stored with the specified key.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <returns>
        /// The registered object, or null is there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime);

        /// <summary>
        /// Unregisters a previously-registered task object.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <returns>
        /// The registered object, or null is there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime);
    }
}
