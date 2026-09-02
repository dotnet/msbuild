// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

#nullable disable

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
    /// <remarks>
    /// A registered-task-object store cannot be shared across processes. Build-lifetime objects
    /// belong to the build component host, while AppDomain-lifetime objects are shared within the
    /// AppDomain that hosts MSBuild. Worker processes in a multiprocess build therefore have
    /// independent stores. In a multithreaded build, in-process tasks share a component host and
    /// its store across thread nodes, while tasks routed to a TaskHost use stores in that TaskHost.
    /// </remarks>
    public interface IBuildEngine4 : IBuildEngine3
    {
        /// <summary>
        /// Registers an object with the system that will be disposed of at some specified time
        /// in the future.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="obj">The object to be held for later disposal.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <param name="allowEarlyCollection">The object may be disposed earlier than the requested time if
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
        /// A registered object cannot be retrieved from another process. Worker processes in a
        /// multiprocess build have independent stores, so registered objects are not a cross-node
        /// communication mechanism. In a multithreaded build, in-process tasks share the build
        /// component host and its store across thread nodes. Tasks routed to a TaskHost use stores
        /// in the TaskHost process.
        /// </para>
        /// <para>
        /// Store operations are thread-safe, but registered objects are not made thread-safe.
        /// An object shared by in-process tasks in a multithreaded build may be accessed
        /// concurrently and must provide any required synchronization. Compound operations,
        /// such as retrieving an object and then registering one when none exists, are not atomic.
        /// </para>
        /// <para>
        /// A registration does not replace an object already registered with an equal key and
        /// the same lifetime. Keys are shared by all tasks using the same process-local store.
        /// Use a private, unique key for task-private state, or a deliberately coordinated key
        /// for state that is intended to be shared.
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
        /// The registered object, or null if there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        /// <remarks>
        /// The lookup cannot retrieve an object from another process or build component host. See
        /// <see cref="RegisterTaskObject"/> for store scope and concurrency considerations.
        /// </remarks>
        object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime);

        /// <summary>
        /// Unregisters a previously-registered task object.
        /// </summary>
        /// <param name="key">The key used to retrieve the object.</param>
        /// <param name="lifetime">The lifetime of the object.</param>
        /// <returns>
        /// The registered object, or null if there is no object registered under that key or the object
        /// has been discarded through early collection.
        /// </returns>
        /// <remarks>
        /// The operation cannot unregister an object from another process or build component host. See
        /// <see cref="RegisterTaskObject"/> for store scope and concurrency considerations.
        /// </remarks>
        object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime);
    }
}
