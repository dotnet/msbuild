// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external construction objects derived from <see cref="ProjectElementContainer"/>
    /// </summary>
    public abstract class ProjectElementContainerLink : ProjectElementLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectElementContainer.Count"/>.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElementContainer.FirstChild"/>.
        /// </summary>
        public abstract ProjectElement FirstChild { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElementContainer.LastChild"/>.
        /// </summary>
        public abstract ProjectElement LastChild { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectElementContainer.InsertAfterChild"/>.
        /// </summary>
        public abstract void InsertAfterChild(ProjectElement child, ProjectElement reference);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectElementContainer.InsertBeforeChild"/>.
        /// </summary>
        public abstract void InsertBeforeChild(ProjectElement child, ProjectElement reference);

        /// <summary>
        /// Helps implementation of the <see cref="ProjectElementContainer.AppendChild"/>.
        /// </summary>
        public abstract void AddInitialChild(ProjectElement child);

        /// <summary>
        /// helps implementation the <see cref="ProjectElementContainer.DeepCopyFrom"/>.
        /// </summary>
        public abstract ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectElementContainer.RemoveChild"/>.
        /// </summary>
        public abstract void RemoveChild(ProjectElement child);

        /// <summary>
        /// ExternalProjectsProvider helpers
        /// </summary>
        public static void AddInitialChild(ProjectElementContainer xml, ProjectElement child) => xml.AddInitialChild(child);
        public static ProjectElementContainer DeepClone(ProjectElementContainer xml, ProjectRootElement factory, ProjectElementContainer parent) => ProjectElementContainer.DeepClone(xml, factory, parent);
    }

    // the "equivalence" classes in cases when we don't need additional functionality,
    // but want to allow for such to be added in the future.

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectChooseElement"/>
    /// </summary>
    public abstract class ProjectChooseElementLink : ProjectElementContainerLink  { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectImportGroupElement"/>
    /// </summary>
    public abstract class ProjectImportGroupElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItemDefinitionElement"/>
    /// </summary>
    public abstract class ProjectItemDefinitionElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItemDefinitionGroupElement"/>
    /// </summary>
    public abstract class ProjectItemDefinitionGroupElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectItemGroupElement"/>
    /// </summary>
    public abstract class ProjectItemGroupElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectOtherwiseElement"/>
    /// </summary>
    public abstract class ProjectOtherwiseElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectPropertyGroupElement"/>
    /// </summary>
    public abstract class ProjectPropertyGroupElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectSdkElement"/>
    /// </summary>
    public abstract class ProjectSdkElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectUsingTaskElement"/>
    /// </summary>
    public abstract class ProjectUsingTaskElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectWhenElement"/>
    /// </summary>
    public abstract class ProjectWhenElementLink : ProjectElementContainerLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="UsingTaskParameterGroupElement"/>
    /// </summary>
    public abstract class UsingTaskParameterGroupElementLink : ProjectElementContainerLink { }
}
