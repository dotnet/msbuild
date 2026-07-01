// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Resources;

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This helper base class provides default functionality for tasks. This class can only be instantiated in a derived form.
    /// </summary>
    public abstract class Task : ITask
    {
        #region Constructors

        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected Task()
        {
            Log = new TaskLoggingHelper(this);
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        protected Task(ResourceManager taskResources)
            : this()
        {
            Log.TaskResources = taskResources;
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources, as well as provide a prefix for
        /// composing help keywords from string resource names. If the prefix is an empty string, then string resource names will
        /// be used verbatim as help keywords. For an example of how the prefix is used, see the
        /// TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[]) method.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix.</param>
        protected Task(ResourceManager taskResources, string helpKeywordPrefix)
            : this(taskResources)
        {
            Log.HelpKeywordPrefix = helpKeywordPrefix;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The build engine automatically sets this property to allow tasks to call back into it.
        /// </summary>
        /// <value>The build engine interface available to tasks.</value>
        public IBuildEngine BuildEngine { get; set; }

        // The casts below are always possible because this class is built against the
        // Orcas Framework assembly or later, so the version of MSBuild that does not
        // know about IBuildEngine2 will never load it.
        // No setters needed; the Engine always sets through the BuildEngine setter

        /// <summary>
        /// The build engine automatically sets this property to allow tasks to call back into it.
        /// This is a convenience property so that task authors inheriting from this class do not
        /// have to cast the value from IBuildEngine to IBuildEngine2.
        /// </summary>
        /// <value>The build engine interface available to tasks.</value>
        public IBuildEngine2 BuildEngine2 => (IBuildEngine2)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine3" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine3 BuildEngine3 => (IBuildEngine3)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine4" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine4 BuildEngine4 => (IBuildEngine4)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine5" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine5 BuildEngine5 => (IBuildEngine5)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine6" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine6 BuildEngine6 => (IBuildEngine6)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine7" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine7 BuildEngine7 => (IBuildEngine7)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine8" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine8 BuildEngine8 => (IBuildEngine8)BuildEngine;

        /// <summary>
        /// Retrieves the <see cref="IBuildEngine9" /> version of the build engine interface provided by the host.
        /// </summary>
        public IBuildEngine9 BuildEngine9 => (IBuildEngine9)BuildEngine;

        /// <summary>
        /// The build engine sets this property if the host IDE has associated a host object with this particular task.
        /// </summary>
        /// <value>The host object instance (can be null).</value>
        public ITaskHost HostObject { get; set; }

        /// <summary>
        /// Gets an instance of a TaskLoggingHelper class containing task logging methods.
        /// The taskLoggingHelper is a MarshallByRef object which needs to have MarkAsInactive called
        /// if the parent task is making the appdomain and marshaling this object into it. If the appdomain is not unloaded at the end of
        /// the task execution and the MarkAsInactive method is not called this will result in a leak of the task instances in the appdomain the task was created within.
        /// </summary>
        /// <value>The logging helper object.</value>
        public TaskLoggingHelper Log { get; }

        /// <summary>
        /// Gets or sets the task's culture-specific resources. Derived classes should register their resources either during
        /// construction, or via this property, if they have localized strings.
        /// </summary>
        /// <value>The task's resources (can be null).</value>
        protected ResourceManager TaskResources
        {
            get => Log.TaskResources;
            set => Log.TaskResources = value;
        }

        /// <summary>
        /// Gets or sets the prefix used to compose help keywords from string resource names. If a task does not have help
        /// keywords associated with its messages, it can ignore this property or set it to null. If the prefix is set to an empty
        /// string, then string resource names will be used verbatim as help keywords. For an example of how this prefix is used,
        /// see the TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[]) method.
        /// </summary>
        /// <value>The help keyword prefix string (can be null).</value>
        protected string HelpKeywordPrefix
        {
            get => Log.HelpKeywordPrefix;
            set => Log.HelpKeywordPrefix = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Must be implemented by derived class.
        /// </summary>
        /// <returns>true, if successful</returns>
        public abstract bool Execute();

        #endregion

        #region Task class registration

        /// <summary>
        /// Registers a task type under the name a target uses to invoke it (the <c>TaskName</c> of a
        /// <c>&lt;UsingTask&gt;</c>), so MSBuild can instantiate and run it without loading its assembly or
        /// resolving its type by reflection - the path required in a trimmed or Native AOT host.
        /// </summary>
        /// <typeparam name="T">
        /// The task type to register. It must have a public parameterless constructor. The
        /// <c>[DynamicallyAccessedMembers]</c> roots the type's public constructor and properties so a
        /// trimmer preserves them, keeping both construction and parameter binding working.
        /// </typeparam>
        /// <param name="taskName">
        /// The name a target uses to invoke the task. This is the <c>TaskName</c> of the corresponding
        /// <c>&lt;UsingTask&gt;</c> (typically the task's class name, optionally namespace-qualified).
        /// </param>
        /// <remarks>
        /// Intended to be called once per task during host initialization, before the first build. This
        /// method is thread-safe; registering the same name again replaces the previous registration. A
        /// registered name takes precedence over a project-level <c>&lt;UsingTask&gt;</c> of the same name,
        /// and registration does not participate in <c>Runtime</c>/<c>Architecture</c> task-identity
        /// selection - the registered task is always the one the engine constructs.
        /// </remarks>
        public static void RegisterTask<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
            string taskName)
            where T : ITask, new()
            => TaskClassRegistry.Register<T>(taskName);

        /// <summary>
        /// Registers a task under the name a target uses to invoke it (the <c>TaskName</c> of a
        /// <c>&lt;UsingTask&gt;</c>) with an explicit factory, so construction is fully reflection-free (the
        /// host supplies the constructor). Use this for tasks without a public parameterless constructor or
        /// that need custom construction.
        /// </summary>
        /// <param name="taskName">
        /// The name a target uses to invoke the task. This is the <c>TaskName</c> of the corresponding
        /// <c>&lt;UsingTask&gt;</c> (typically the task's class name, optionally namespace-qualified).
        /// </param>
        /// <param name="factory">A delegate that creates a new instance of the task.</param>
        /// <remarks>
        /// <para>
        /// Construction is reflection-free, but binding the task's parameters still reflects over its
        /// properties. Because the task type is not statically known through this overload, the host is
        /// responsible for preserving that type's public properties under trimming (for example by also
        /// registering it through <see cref="RegisterTask{T}(string)"/>, which roots them). The generic
        /// overload is the fully trim-safe path.
        /// </para>
        /// <para>
        /// Intended to be called once per task during host initialization, before the first build. This
        /// method is thread-safe; registering the same name again replaces the previous registration. A
        /// registered name takes precedence over a project-level <c>&lt;UsingTask&gt;</c> of the same name.
        /// </para>
        /// </remarks>
        public static void RegisterTask(string taskName, Func<ITask> factory)
            => TaskClassRegistry.Register(taskName, factory);

        #endregion
    }
}
