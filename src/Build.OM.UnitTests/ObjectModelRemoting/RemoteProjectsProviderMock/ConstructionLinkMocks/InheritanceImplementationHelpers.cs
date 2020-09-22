// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;
    /// <summary>
    /// The C# does not really provide a easy way to efficiently implement inheritance in cases like this
    /// for abstract classes or interface, when there is a hierarchy, it is not way to share the implementation.
    /// Like if one have IFoo and IBar : IFoo (or as we do abstractFoo, abstractBar:abstractFoo) 
    /// we can provide implementation for IFoo, but we can not use that for implementations for IBar.
    /// Since no multiple inheritance or other suitable mechanism for code share across classes is supported by C#,
    /// Instead IBar implementation should fully implement both IFoo and IBar interfaces.
    ///
    /// For construction model we do have a clear hierarchy like "Object" [: ProjectElementContainer] : ProjectElement
    /// that for the purpose of linkig is supported via ObjectLink[:ProjectElementContainer]:ProjectElementLink.
    /// Now implementation of all ProjectElementLink and ProjectElementContainer link is in fact identical, but each "ObjectLink" needs to implement it separately.
    ///
    ///
    /// This approach with extension methods helps us put all implementation in one place, and only standard copy and pace "hookup" is needed for each classes.
    /// </summary>
    internal interface IProjectElementContainerLinkHelper
    {
        ProjectCollectionLinker Linker { get; }
        MockProjectElementContainerLinkRemoter ContainerProxy { get; }
    }

    internal interface IProjectElementLinkHelper
    {
        ProjectCollectionLinker Linker { get; }
        MockProjectElementLinkRemoter ElementProxy { get; }
    }

    internal static class InheritanceImplementationHelpers
    {
        // this is so we dont use ?. everywhere.
        // for null remoters the local object is always null
        public static ProjectElement Import(this MockProjectElementLinkRemoter remoter, ProjectCollectionLinker remote)
        {
            if (remoter == null)
            {
                return null;
            }

            return remoter.ImportImpl(remote);
        }

        #region ProjectElementLink implementation
        public static ProjectElementContainer GetParent(this IProjectElementLinkHelper xml)
        {
            return (ProjectElementContainer)xml.ElementProxy.Parent.Import(xml.Linker);
        }

        public static ProjectRootElement GetContainingProject(this IProjectElementLinkHelper xml)
        {
            return (ProjectRootElement)xml.ElementProxy.ContainingProject.Import(xml.Linker);
        }

        public static string GetElementName(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.ElementName;
        }

        public static string GetOuterElement(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.OuterElement;
        }

        public static bool GetExpressedAsAttribute(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.ExpressedAsAttribute;
        }

        public static void SetExpressedAsAttribute(this IProjectElementLinkHelper xml, bool value)
        {
            xml.ElementProxy.ExpressedAsAttribute = value;
        }
        public static ProjectElement GetPreviousSibling(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.PreviousSibling.Import(xml.Linker);
        }

        public static ProjectElement GetNextSibling(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.NextSibling.Import(xml.Linker);
        }

        public static ElementLocation GetLocation(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.Location;
        }

        public static IReadOnlyCollection<XmlAttributeLink> GetAttributes(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.Attributes;
        }

        public static string GetPureText(this IProjectElementLinkHelper xml)
        {
            return xml.ElementProxy.PureText;
        }

        public static void CopyFrom(this IProjectElementLinkHelper xml, ProjectElement element)
        {
            xml.ElementProxy.CopyFrom(xml.Linker.ExportElement(element));
        }

        public static ProjectElement CreateNewInstance(this IProjectElementLinkHelper xml, ProjectRootElement owner)
        {
            return xml.ElementProxy.CreateNewInstance(xml.Linker.Export<ProjectElement, MockProjectRootElementLinkRemoter>(owner)).Import(xml.Linker);
        }

        public static ElementLocation GetAttributeLocation(this IProjectElementLinkHelper xml, string attributeName)
        {
            return xml.ElementProxy.GetAttributeLocation(attributeName);
        }

        public static string GetAttributeValue(this IProjectElementLinkHelper xml, string attributeName, bool nullIfNotExists)
        {
            return xml.ElementProxy.GetAttributeValue(attributeName, nullIfNotExists);
        }

        public static void SetOrRemoveAttribute(this IProjectElementLinkHelper xml, string name, string value, bool allowSettingEmptyAttributes, string reason, string param)
        {
            xml.ElementProxy.SetOrRemoveAttribute(name, value, allowSettingEmptyAttributes, reason, param);
        }
        #endregion

        #region ProjectElementContainerLink implementation
        public static int GetCount(this IProjectElementContainerLinkHelper xml)
        {
            return xml.ContainerProxy.Count;
        }

        public static ProjectElement GetFirstChild(this IProjectElementContainerLinkHelper xml)
        {
            return xml.ContainerProxy.FirstChild.Import(xml.Linker);
        }

        public static ProjectElement GetLastChild(this IProjectElementContainerLinkHelper xml)
        {
            return xml.ContainerProxy.LastChild.Import(xml.Linker);
        }

        public static void InsertAfterChild(this IProjectElementContainerLinkHelper xml, ProjectElement child, ProjectElement reference)
        {
            var childRemote = xml.Linker.ExportElement(child);
            var referenceRemote = xml.Linker.ExportElement(reference);
            xml.ContainerProxy.InsertAfterChild(childRemote, referenceRemote);
        }
        public static void InsertBeforeChild(this IProjectElementContainerLinkHelper xml, ProjectElement child, ProjectElement reference)
        {
            var childRemote = xml.Linker.ExportElement(child);
            var referenceRemote = xml.Linker.ExportElement(reference);
            xml.ContainerProxy.InsertBeforeChild(childRemote, referenceRemote);
        }
        public static void AddInitialChild(this IProjectElementContainerLinkHelper xml, ProjectElement child)
        {
            var childRemote = xml.Linker.ExportElement(child);
            xml.ContainerProxy.AddInitialChild(childRemote);
        }
        public static ProjectElementContainer DeepClone(this IProjectElementContainerLinkHelper xml, ProjectRootElement factory, ProjectElementContainer parent)
        {
            var factoryRemote = xml.Linker.Export<ProjectElement, MockProjectRootElementLinkRemoter>(factory);
            var parentRemote = (MockProjectElementContainerLinkRemoter)xml.Linker.ExportElement(parent);
            var result  = xml.ContainerProxy.DeepClone(factoryRemote, parentRemote);

            return (ProjectElementContainer)result.Import(xml.Linker);
        }

        public static void RemoveChild(this IProjectElementContainerLinkHelper xml, ProjectElement child)
        {
            xml.ContainerProxy.RemoveChild(xml.Linker.ExportElement(child));
        }

        #endregion
    }
}
