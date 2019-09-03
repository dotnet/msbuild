// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Xml;
using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// This interface will allow us to share a single field between
    /// <see cref="XmlElementWithLocation"/> and <see cref="ProjectElementLink"/>
    /// for construction objects so therefore not increasing the storage size while supporting
    /// external linking.
    /// <see cref="ProjectElement.XmlElement"/> and <see cref="ProjectElement.Link"/>
    /// </summary>
    internal interface ILinkedXml
    {
        /// <summary>
        /// Not null for "external" objects, null for internal objects
        /// </summary>
        ProjectElementLink Link { get; }

        /// <summary>
        /// Null for "external" objects, not null for internal objects
        /// </summary>
        XmlElementWithLocation Xml { get; }
    }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external construction objects derived from <see cref="ProjectElement"/>
    /// </summary>
    public abstract class ProjectElementLink : ILinkedXml
    {
        /// <summary>
        /// <see cref="ILinkedXml.Link"/>
        /// </summary>
        ProjectElementLink ILinkedXml.Link => this;

        /// <summary>
        /// <see cref="ILinkedXml.Xml"/>
        /// </summary>
        XmlElementWithLocation ILinkedXml.Xml => null;

        /// <summary>
        /// Access to remote <see cref="ProjectElement.Parent"/>.
        /// </summary>
        public abstract ProjectElementContainer Parent { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.ContainingProject"/>.
        /// </summary>
        public abstract ProjectRootElement ContainingProject { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.ElementName"/>.
        /// </summary>
        public abstract string ElementName { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.OuterElement"/>.
        /// </summary>
        public abstract string OuterElement { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.ExpressedAsAttribute"/>.
        /// </summary>
        public abstract bool ExpressedAsAttribute { get; set; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.PreviousSibling"/>.
        /// </summary>
        public abstract ProjectElement PreviousSibling { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.NextSibling"/>.
        /// </summary>
        public abstract ProjectElement NextSibling { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectElement.Location"/>.
        /// </summary>
        public abstract ElementLocation Location { get; }

        /// <summary>
        /// Supports <see cref="ProjectElement.CopyFrom"/>.
        /// </summary>
        public abstract IReadOnlyCollection<XmlAttributeLink> Attributes { get; }

        /// <summary>
        /// Supports <see cref="ProjectElement.CopyFrom"/>.
        /// return raw xml content of the element if it has exactly 1 text child
        /// </summary>
        public abstract string PureText { get; }

        /// <summary>
        /// Required to implement Attribute access for remote element.
        /// </summary>
        public abstract ElementLocation GetAttributeLocation(string attributeName);

        /// <summary>
        /// Required to implement Attribute access for remote element.
        /// </summary>
        public abstract string GetAttributeValue(string attributeName, bool nullIfNotExists);

        /// <summary>
        /// Required to implement Attribute access for remote element.
        /// </summary>
        public abstract void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param);

        /// <summary>
        /// Facilitate remoting to remote <see cref="ProjectElement.CopyFrom"/>.
        /// </summary>
        public abstract void CopyFrom(ProjectElement element);

        /// <summary>
        /// Facilitate remoting to remote <see cref="ProjectElement.CreateNewInstance(ProjectRootElement)"/>.
        /// </summary>
        public abstract ProjectElement CreateNewInstance(ProjectRootElement owner);

        /// <summary>
        /// Utility function for ExternalProjects provider
        /// </summary>
        public static bool GetExpressedAsAttribute(ProjectElement xml) => xml.ExpressedAsAttribute;
        public static void SetExpressedAsAttribute(ProjectElement xml, bool value) => xml.ExpressedAsAttribute = value;
        public static ElementLocation GetAttributeLocation(ProjectElement xml, string attributeName) => xml.GetAttributeLocation(attributeName);
        public static string GetAttributeValue(ProjectElement xml, string attributeName, bool nullIfNotExists) => xml.GetAttributeValue(attributeName, nullIfNotExists);
        public static void SetOrRemoveAttribute(ProjectElement xml, string name, string value, bool clearAttributeCache, string reason, string param) => xml.SetOrRemoveAttributeForLink(name, value, clearAttributeCache, reason, param);
        public static void MarkDirty(ProjectElement xml, string reason, string param) => xml.MarkDirty(reason, param);
        public static ProjectElement CreateNewInstance(ProjectElement xml, ProjectRootElement owner) =>  ProjectElement.CreateNewInstance(xml, owner);

        public static string GetPureText(ProjectElement xml)
        {
            string result = null;
            if (xml.XmlElement.ChildNodes.Count == 1 && xml.XmlElement.FirstChild.NodeType == XmlNodeType.Text)
            {
                result = xml.XmlElement.FirstChild.Value;
            }

            return result;
        }
        public static IReadOnlyCollection<XmlAttributeLink> GetAttributes(ProjectElement xml)
        {
            List<XmlAttributeLink> result = new List<XmlAttributeLink>();
            foreach (XmlAttribute attribute in xml.XmlElement.Attributes)
            {
                result.Add(new XmlAttributeLink(attribute.LocalName, attribute.Value, attribute.NamespaceURI));
            }

            return result;
        }
    }

    public struct XmlAttributeLink
    {
        public XmlAttributeLink(string localName, string value, string namespaceUri)
        {
            this.LocalName = localName;
            this.Value = value;
            this.NamespaceURI = namespaceUri;
        }

        public string LocalName { get; }
        public string Value { get; }
        public string NamespaceURI { get; }
    }

    // the "equivalence" classes in cases when we don't need additional functionality,
    // but want to allow for such to be added in the future.

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectOnErrorElement"/>
    /// </summary>
    public abstract class ProjectOnErrorElementLink : ProjectElementLink { }

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectOutputElement"/>
    /// </summary>
    public abstract class ProjectOutputElementLink : ProjectElementLink { }
}
