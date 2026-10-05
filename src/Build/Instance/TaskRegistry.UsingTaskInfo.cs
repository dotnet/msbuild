// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Execution;

internal sealed partial class TaskRegistry
{
    /// <summary>
    ///  Contains the fully evaluated data needed to register one <see cref="ProjectUsingTaskElement"/>.
    /// </summary>
    /// <remarks>
    ///  Creating this value expands and validates the element's attributes, resolves its assembly load identity,
    ///  expands any inline-task definition, and applies compatibility redirects for built-in task factories.
    ///  Keeping that work together prevents partially evaluated registration state from escaping into
    ///  <see cref="TaskRegistry"/>.
    /// </remarks>
    private readonly struct UsingTaskInfo
    {
        /// <summary>
        ///  The simple name of the MSBuild v4 Tasks assembly.
        /// </summary>
        private const string TasksV4SimpleName = "Microsoft.Build.Tasks.v4.0";

        /// <summary>
        ///  The prefix shared by partial and fully qualified names for the MSBuild v4 Tasks assembly.
        /// </summary>
        private const string TasksV4AssemblyNamePrefix = $"{TasksV4SimpleName},";

        /// <summary>
        ///  The filename of the MSBuild v4 Tasks assembly.
        /// </summary>
        private const string TasksV4Filename = $"{TasksV4SimpleName}.dll";

        /// <summary>
        ///  The simple name of the MSBuild v12 Tasks assembly.
        /// </summary>
        private const string TasksV12SimpleName = "Microsoft.Build.Tasks.v12.0";

        /// <summary>
        ///  The prefix shared by partial and fully qualified names for the MSBuild v12 Tasks assembly.
        /// </summary>
        private const string TasksV12AssemblyNamePrefix = $"{TasksV12SimpleName},";

        /// <summary>
        ///  The filename of the MSBuild v12 Tasks assembly.
        /// </summary>
        private const string TasksV12Filename = $"{TasksV12SimpleName}.dll";

        /// <summary>
        ///  The simple name of the current MSBuild Tasks assembly.
        /// </summary>
        private const string TasksCoreSimpleName = "Microsoft.Build.Tasks.Core";

        /// <summary>
        ///  The filename of the current MSBuild Tasks assembly.
        /// </summary>
        private const string TasksCoreFilename = $"{TasksCoreSimpleName}.dll";

        /// <summary>
        ///  The expected location of the MSBuild v4 Tasks assembly when referenced by simple name.
        /// </summary>
        private static readonly string s_potentialTasksV4Location =
            Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, TasksV4Filename);

        /// <summary>
        ///  The expected location of the MSBuild v12 Tasks assembly when referenced by simple name.
        /// </summary>
        private static readonly string s_potentialTasksV12Location =
            Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, TasksV12Filename);

        /// <summary>
        ///  The official strong name of the MSBuild v4 Tasks assembly.
        /// </summary>
        private static readonly AssemblyNameExtension s_tasksV4AssemblyName =
            new($"{TasksV4SimpleName}, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        /// <summary>
        ///  The official strong name of the MSBuild v12 Tasks assembly.
        /// </summary>
        private static readonly AssemblyNameExtension s_tasksV12AssemblyName =
            new($"{TasksV12SimpleName}, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        /// <summary>
        ///  The expected location of the current MSBuild Tasks assembly.
        /// </summary>
        private static readonly string s_potentialTasksCoreLocation =
            Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, TasksCoreFilename);

        /// <summary>
        ///  The evaluated task name.
        /// </summary>
        public readonly string TaskName;

        /// <summary>
        ///  The evaluated task-factory name.
        /// </summary>
        public readonly string TaskFactory;

        /// <summary>
        ///  The final assembly identity after path normalization and compatibility redirects.
        /// </summary>
        public readonly AssemblyLoadInfo LoadInfo;

        /// <summary>
        ///  The evaluated runtime and architecture constraints for the task factory.
        /// </summary>
        public readonly TaskHostParameters TaskFactoryParameters;

        /// <summary>
        ///  The evaluated parameter group and task body for an inline task, or <see langword="null"/> when the
        ///  <see cref="ProjectUsingTaskElement"/> has no child elements.
        /// </summary>
        public readonly RegisteredTaskRecord.ParameterGroupAndTaskElementRecord? InlineTaskRecord;

        /// <summary>
        ///  Whether this registration overrides earlier registrations for the same task.
        /// </summary>
        public readonly bool OverrideTask;

        private UsingTaskInfo(
            string taskName,
            string taskFactory,
            AssemblyLoadInfo loadInfo,
            TaskHostParameters taskFactoryParameters,
            RegisteredTaskRecord.ParameterGroupAndTaskElementRecord? inlineTaskRecord,
            bool overrideTask)
        {
            TaskName = taskName;
            TaskFactory = taskFactory;
            LoadInfo = loadInfo;
            TaskFactoryParameters = taskFactoryParameters;
            InlineTaskRecord = inlineTaskRecord;
            OverrideTask = overrideTask;
        }

        /// <summary>
        ///  Evaluates a <see cref="ProjectUsingTaskElement"/> into the data required by the task registry.
        /// </summary>
        /// <typeparam name="TProperty">The property type consumed by <paramref name="expander"/>.</typeparam>
        /// <typeparam name="TItem">The item type consumed by <paramref name="expander"/>.</typeparam>
        /// <param name="projectUsingTaskXml">The unevaluated <c>UsingTask</c> element.</param>
        /// <param name="expander">The expander used to evaluate element attributes and inline-task content.</param>
        /// <param name="expanderOptions">The options controlling expansion.</param>
        /// <param name="fileSystem">The file system used when applying task-factory compatibility policies.</param>
        /// <param name="directoryOfImportingFile">The directory against which relative assembly paths are resolved.</param>
        /// <returns>
        ///  A fully evaluated task registration.
        /// </returns>
        /// <remarks>
        ///  Attribute and child-element expansion intentionally follows the historical registration order so that
        ///  invalid projects continue to report the same first error.
        /// </remarks>
        public static UsingTaskInfo Create<TProperty, TItem>(
            ProjectUsingTaskElement projectUsingTaskXml,
            Expander<TProperty, TItem> expander,
            ExpanderOptions expanderOptions,
            IFileSystem fileSystem,
            string directoryOfImportingFile)
            where TProperty : class, IProperty
            where TItem : class, IItem
        {
            Processor<TProperty, TItem> processor = new(projectUsingTaskXml, expander, expanderOptions, fileSystem, directoryOfImportingFile);

            string taskName = processor.GetTaskName();
            string taskFactory = processor.GetTaskFactory();
            AssemblyLoadInfo loadInfo = processor.GetLoadInfo(taskFactory);

            RegisteredTaskRecord.ParameterGroupAndTaskElementRecord? inlineTaskRecord;

            if (projectUsingTaskXml.Count > 0)
            {
                inlineTaskRecord = new RegisteredTaskRecord.ParameterGroupAndTaskElementRecord();
                inlineTaskRecord.ExpandUsingTask(projectUsingTaskXml, expander, expanderOptions);
            }
            else
            {
                inlineTaskRecord = null;
            }

            TaskHostParameters taskFactoryParameters;
            string runtime = processor.GetRuntime();
            string architecture = processor.GetArchitecture();

            taskFactoryParameters = (runtime != string.Empty) || (architecture != string.Empty)
                ? new TaskHostParameters(
                    runtime == string.Empty ? XMakeAttributes.MSBuildRuntimeValues.any : runtime,
                    architecture == string.Empty ? XMakeAttributes.MSBuildArchitectureValues.any : architecture)
                : TaskHostParameters.Empty;

            bool overrideUsingTask = processor.GetOverrideTask();

            return new(taskName, taskFactory, loadInfo, taskFactoryParameters, inlineTaskRecord, overrideUsingTask);
        }

        /// <summary>
        ///  Determines whether a path names one of the legacy Microsoft Build Tasks assemblies.
        /// </summary>
        /// <param name="assemblyFile">The assembly path to inspect.</param>
        /// <returns>
        ///  <see langword="true"/> when the final path component is an MSBuild v4 or v12 Tasks filename; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        private static bool IsLegacyBuildTasksAssemblyFile([NotNullWhen(true)] string? assemblyFile)
            => HasFileName(assemblyFile, TasksV4Filename)
            || HasFileName(assemblyFile, TasksV12Filename);

        /// <summary>
        ///  Determines whether the final component of a path exactly matches a filename without allocating a
        ///  separate filename string.
        /// </summary>
        /// <param name="path">The path to inspect.</param>
        /// <param name="fileName">The filename to match.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="path"/> ends with the complete <paramref name="fileName"/>
        ///  component; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool HasFileName([NotNullWhen(true)] string? path, string fileName)
        {
            if (path is null || !path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int fileNameStart = path.Length - fileName.Length;
            return fileNameStart == 0 || FileUtilities.IsSlash(path[fileNameStart - 1]);
        }

        /// <summary>
        ///  Determines whether an assembly display name identifies an official legacy Microsoft Build Tasks assembly.
        /// </summary>
        /// <param name="assemblyName">The simple, partial, or fully qualified assembly name to inspect.</param>
        /// <returns>
        ///  <see langword="true"/> for a compatible MSBuild v4 or v12 Tasks identity; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Partial display names are accepted only when every supplied identity component agrees with the official
        ///  assembly identity.
        /// </remarks>
        private static bool IsLegacyBuildTasksAssemblyName([NotNullWhen(true)] string? assemblyName)
        {
            if (assemblyName is null)
            {
                return false;
            }

            if (assemblyName.Equals(TasksV4SimpleName, StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Equals(TasksV12SimpleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!assemblyName.StartsWith(TasksV4AssemblyNamePrefix, StringComparison.OrdinalIgnoreCase) &&
                !assemblyName.StartsWith(TasksV12AssemblyNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var requestedAssemblyName = new AssemblyNameExtension(assemblyName);

                return requestedAssemblyName.PartialNameCompare(s_tasksV4AssemblyName)
                    || requestedAssemblyName.PartialNameCompare(s_tasksV12AssemblyName);
            }
            catch (FileLoadException)
            {
                // Leave malformed display names unchanged so normal assembly loading reports the established error.
                return false;
            }
        }

        /// <summary>
        ///  Expands and validates one <see cref="ProjectUsingTaskElement"/> against its evaluation context.
        /// </summary>
        /// <typeparam name="TProperty">The property type consumed by <paramref name="expander"/>.</typeparam>
        /// <typeparam name="TItem">The item type consumed by <paramref name="expander"/>.</typeparam>
        /// <param name="projectUsingTaskXml">The element being processed.</param>
        /// <param name="expander">The expander used to evaluate its values.</param>
        /// <param name="expanderOptions">The options controlling expansion.</param>
        /// <param name="fileSystem">The file system used for task-factory compatibility checks.</param>
        /// <param name="directoryOfImportingFile">The base directory for relative assembly paths.</param>
        /// <remarks>
        ///  This stack-only helper keeps the source element and evaluation services together without allocating a
        ///  processor object for every task registration.
        /// </remarks>
        private readonly ref struct Processor<TProperty, TItem>(
            ProjectUsingTaskElement projectUsingTaskXml,
            Expander<TProperty, TItem> expander,
            ExpanderOptions expanderOptions,
            IFileSystem fileSystem,
            string directoryOfImportingFile)
            where TProperty : class, IProperty
            where TItem : class, IItem
        {
            /// <summary>
            ///  Expands and validates the required task name.
            /// </summary>
            /// <returns>
            ///  The evaluated task name.
            /// </returns>
            public string GetTaskName()
                => GetValidAttributeValue(XMakeAttributes.name, projectUsingTaskXml.TaskName, projectUsingTaskXml.TaskNameLocation);

            /// <summary>
            ///  Expands the task-factory name and rejects child elements for
            ///  <c>AssemblyTaskFactory</c> and <c>TaskHostFactory</c>.
            /// </summary>
            /// <returns>
            ///  The evaluated task-factory name.
            /// </returns>
            public string GetTaskFactory()
            {
                string taskFactory = Expand(projectUsingTaskXml.TaskFactory, projectUsingTaskXml.TaskFactoryLocation);

                if (taskFactory.IsNullOrEmpty() ||
                    taskFactory.Equals(RegisteredTaskRecord.AssemblyTaskFactory, StringComparison.OrdinalIgnoreCase) ||
                    taskFactory.Equals(RegisteredTaskRecord.TaskHostFactory, StringComparison.OrdinalIgnoreCase))
                {
                    ProjectXmlUtilities.VerifyThrowProjectNoChildElements(
                        projectUsingTaskXml.XmlElement,
                        projectUsingTaskXml.ContainingProject.ProjectRootElementCache.ParserIgnoreConfiguration);
                }

                return taskFactory;
            }

            /// <summary>
            ///  Expands the requested task runtime.
            /// </summary>
            /// <returns>
            ///  The evaluated runtime, or an empty string when no runtime was specified.
            /// </returns>
            public string GetRuntime()
                => Expand(projectUsingTaskXml.Runtime, projectUsingTaskXml.RuntimeLocation);

            /// <summary>
            ///  Expands the requested task architecture.
            /// </summary>
            /// <returns>
            ///  The evaluated architecture, or an empty string when no architecture was specified.
            /// </returns>
            public string GetArchitecture()
                => Expand(projectUsingTaskXml.Architecture, projectUsingTaskXml.ArchitectureLocation);

            /// <summary>
            ///  Expands and interprets the <c>Override</c> attribute.
            /// </summary>
            /// <returns>
            ///  <see langword="true"/> when the attribute has an accepted true value; otherwise,
            ///  <see langword="false"/>.
            /// </returns>
            public bool GetOverrideTask()
                => ConversionUtilities.ValidBooleanTrue(Expand(projectUsingTaskXml.Override, projectUsingTaskXml.OverrideLocation));

            /// <summary>
            ///  Resolves the assembly identity used to load the task or task factory.
            /// </summary>
            /// <param name="taskFactory">The evaluated task-factory name.</param>
            /// <returns>
            ///  The normalized assembly load information, including any applicable compatibility redirect.
            /// </returns>
            /// <remarks>
            ///  Assembly files are resolved relative to the file that declared the <c>UsingTask</c>. Compatibility
            ///  redirects are then applied independently for <c>CodeTaskFactory</c> and <c>XamlTaskFactory</c>.
            /// </remarks>
            public AssemblyLoadInfo GetLoadInfo(string taskFactory)
            {
                string? assemblyFile = null;
                string? assemblyName = null;

                // Project construction guarantees exactly one source attribute. Keep the alternatives separate so
                // AssemblyLoadInfo can preserve whether normal loading should use a path or an assembly display name.
                if (projectUsingTaskXml.AssemblyFile.Length > 0)
                {
                    assemblyFile = ReadAssemblyFile();
                }
                else
                {
                    assemblyName = ReadAssemblyName();
                }

                // Resolve relative paths against the file containing the declaration, not the main project that
                // eventually imported it.
                try
                {
                    assemblyFile = FileUtilities.FixFilePath(assemblyFile);

                    if (assemblyFile != null && !Path.IsPathRooted(assemblyFile))
                    {
                        assemblyFile = Strings.WeakIntern(Path.Combine(directoryOfImportingFile, assemblyFile));
                    }

                    if (string.Equals(taskFactory, RegisteredTaskRecord.CodeTaskFactory, StringComparison.OrdinalIgnoreCase) &&
                        (TryRedirectLegacyCodeTaskFactoryAssemblyFile(assemblyFile, out AssemblyLoadInfo? result) ||
                         TryRedirectLegacyCodeTaskFactoryAssemblyName(assemblyName, out result)))
                    {
                        return result;
                    }

                    if (string.Equals(taskFactory, RegisteredTaskRecord.XamlTaskFactory, StringComparison.OrdinalIgnoreCase) &&
                        (TryRedirectUnavailableLegacyXamlTaskFactoryAssemblyFile(assemblyFile, out result) ||
                         TryRedirectUnavailableLegacyXamlTaskFactoryAssemblyName(assemblyName, out result)))
                    {
                        return result;
                    }

                    return AssemblyLoadInfo.Create(assemblyName, assemblyFile);
                }
                catch (ArgumentException ex)
                {
                    // Translate path API failures into the established UsingTask diagnostic at the declaration site.
                    ProjectErrorUtilities.ThrowInvalidProject(
                        projectUsingTaskXml.Location,
                        "InvalidAttributeValueWithException",
                        assemblyFile,
                        XMakeAttributes.assemblyFile,
                        XMakeElements.usingTask,
                        ex.Message);
                }

                return Assumed.Unreachable<AssemblyLoadInfo>();
            }

            /// <summary>
            ///  Expands and validates the assembly-file attribute.
            /// </summary>
            /// <returns>
            ///  The evaluated assembly file.
            /// </returns>
            private string ReadAssemblyFile()
                => GetValidAttributeValue(XMakeAttributes.assemblyFile, projectUsingTaskXml.AssemblyFile, projectUsingTaskXml.AssemblyFileLocation);

            /// <summary>
            ///  Expands and validates the assembly-name attribute.
            /// </summary>
            /// <returns>
            ///  The evaluated assembly name.
            /// </returns>
            private string ReadAssemblyName()
                => GetValidAttributeValue(XMakeAttributes.assemblyName, projectUsingTaskXml.AssemblyName, projectUsingTaskXml.AssemblyNameLocation);

            /// <summary>
            ///  Expands an attribute whose evaluated value must not be empty.
            /// </summary>
            /// <param name="attributeName">The XML attribute name used in diagnostics.</param>
            /// <param name="attributeValue">The unevaluated attribute value.</param>
            /// <param name="location">The source location used for expansion and diagnostics.</param>
            /// <returns>
            ///  The evaluated attribute value.
            /// </returns>
            private string GetValidAttributeValue(string attributeName, string attributeValue, IElementLocation location)
            {
                string result = Expand(attributeValue, location);

                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    result is null || result.Length > 0,
                    location,
                    "InvalidEvaluatedAttributeValue",
                    result,
                    attributeValue,
                    attributeName,
                    XMakeElements.usingTask);

                return result!;
            }

            /// <summary>
            ///  Expands a value while preserving MSBuild escaping.
            /// </summary>
            /// <param name="text">The text to expand.</param>
            /// <param name="location">The source location used for expansion diagnostics.</param>
            /// <returns>
            ///  The expanded value.
            /// </returns>
            private string Expand(string text, IElementLocation location)
                => expander.ExpandIntoStringLeaveEscaped(text, expanderOptions, location)!;

            /// <summary>
            ///  Redirects a legacy Microsoft <c>CodeTaskFactory</c> assembly path to the current implementation.
            /// </summary>
            /// <param name="assemblyFile">The normalized requested assembly path.</param>
            /// <param name="result">The redirected load information when the method returns <see langword="true"/>.</param>
            /// <returns>
            ///  <see langword="true"/> when a current implementation was found; otherwise, <see langword="false"/>.
            /// </returns>
            /// <remarks>
            ///  Known legacy <c>CodeTaskFactory</c> registrations always prefer the current implementation because
            ///  compiling inline tasks against mixed versions of the MSBuild assemblies can produce ambiguous types.
            /// </remarks>
            private bool TryRedirectLegacyCodeTaskFactoryAssemblyFile(string? assemblyFile, [NotNullWhen(true)] out AssemblyLoadInfo? result)
            {
                if (IsLegacyBuildTasksAssemblyFile(assemblyFile))
                {
                    // Prefer the current MSBuild installation even when the requested legacy assembly exists.
                    if (FileUtilities.FileExistsNoThrow(s_potentialTasksCoreLocation, fileSystem))
                    {
                        result = AssemblyLoadInfo.FromFile(s_potentialTasksCoreLocation);
                        return true;
                    }

                    // Some hosts place Tasks.Core beside the requested legacy assembly instead of under the current
                    // tools directory. Prefer Tasks.Core even when the legacy assembly exists.
                    string replacedAssemblyFile = Path.Combine(Path.GetDirectoryName(assemblyFile)!, TasksCoreFilename);

                    if (FileUtilities.FileExistsNoThrow(replacedAssemblyFile, fileSystem))
                    {
                        result = AssemblyLoadInfo.FromFile(replacedAssemblyFile);
                        return true;
                    }
                }

                result = null;
                return false;
            }

            /// <summary>
            ///  Redirects a legacy Microsoft <c>CodeTaskFactory</c> assembly name to the current implementation.
            /// </summary>
            /// <param name="assemblyName">The requested assembly display name.</param>
            /// <param name="result">The redirected load information when the method returns <see langword="true"/>.</param>
            /// <returns>
            ///  <see langword="true"/> when the name identifies an official legacy Tasks assembly and the current
            ///  implementation is available; otherwise, <see langword="false"/>.
            /// </returns>
            private bool TryRedirectLegacyCodeTaskFactoryAssemblyName(string? assemblyName, [NotNullWhen(true)] out AssemblyLoadInfo? result)
            {
                if (IsLegacyBuildTasksAssemblyName(assemblyName) &&
                    FileUtilities.FileExistsNoThrow(s_potentialTasksCoreLocation, fileSystem))
                {
                    result = AssemblyLoadInfo.FromName(TasksCoreSimpleName);
                    return true;
                }

                result = null;
                return false;
            }

            /// <summary>
            ///  Redirects a missing legacy <c>XamlTaskFactory</c> assembly path to an adjacent current Tasks assembly.
            /// </summary>
            /// <param name="assemblyFile">The normalized requested assembly path.</param>
            /// <param name="result">The redirected load information when the method returns <see langword="true"/>.</param>
            /// <returns>
            ///  <see langword="true"/> when the legacy path is missing and an adjacent current Tasks assembly exists;
            ///  otherwise, <see langword="false"/>.
            /// </returns>
            /// <remarks>
            ///  Unlike <c>CodeTaskFactory</c>, an existing legacy <c>XamlTaskFactory</c> assembly remains unchanged.
            /// </remarks>
            private bool TryRedirectUnavailableLegacyXamlTaskFactoryAssemblyFile(string? assemblyFile, [NotNullWhen(true)] out AssemblyLoadInfo? result)
            {
                if (IsLegacyBuildTasksAssemblyFile(assemblyFile) &&
                    !FileUtilities.FileExistsNoThrow(assemblyFile, fileSystem))
                {
                    // Preserve the historical shim for $(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll and v12.0.
                    string replacedAssemblyFile = Path.Combine(Path.GetDirectoryName(assemblyFile)!, TasksCoreFilename);

                    if (FileUtilities.FileExistsNoThrow(replacedAssemblyFile, fileSystem))
                    {
                        result = AssemblyLoadInfo.FromFile(replacedAssemblyFile);
                        return true;
                    }
                }

                result = null;
                return false;
            }

            /// <summary>
            ///  Redirects a missing simple legacy <c>XamlTaskFactory</c> assembly name to the current Tasks assembly.
            /// </summary>
            /// <param name="assemblyName">The requested assembly name.</param>
            /// <param name="result">The redirected load information when the method returns <see langword="true"/>.</param>
            /// <returns>
            ///  <see langword="true"/> when the request is a missing simple legacy name and the current Tasks assembly
            ///  is available; otherwise, <see langword="false"/>.
            /// </returns>
            private bool TryRedirectUnavailableLegacyXamlTaskFactoryAssemblyName(string? assemblyName, [NotNullWhen(true)] out AssemblyLoadInfo? result)
            {
                if (assemblyName is not null)
                {
                    // Preserve the historical simple-name shim without broadening it to partial or strong names.
                    if (IsMissingSimpleTasksAssembly(assemblyName, fileSystem) &&
                        FileUtilities.FileExistsNoThrow(s_potentialTasksCoreLocation, fileSystem))
                    {
                        result = AssemblyLoadInfo.FromName(TasksCoreSimpleName);
                        return true;
                    }
                }

                result = null;
                return false;

                static bool IsMissingSimpleTasksAssembly(string assemblyName, IFileSystem fileSystem)
                    => (assemblyName.Equals(TasksV4SimpleName, StringComparison.OrdinalIgnoreCase) && !FileUtilities.FileExistsNoThrow(s_potentialTasksV4Location, fileSystem))
                    || (assemblyName.Equals(TasksV12SimpleName, StringComparison.OrdinalIgnoreCase) && !FileUtilities.FileExistsNoThrow(s_potentialTasksV12Location, fileSystem));
            }
        }
    }
}
