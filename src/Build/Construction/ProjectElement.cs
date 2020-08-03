// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Abstract base class for MSBuild construction object model elements. 
    /// </summary>
    public abstract class ProjectElement : IProjectElement, ILinkableObject
    {
        /// <summary>
        /// Parent container object.
        /// </summary>
        private ProjectElementContainer _parent;

        /// <summary>
        /// Condition value cached for performance
        /// </summary>
        private string _condition;

        private bool _expressedAsAttribute;
        private ProjectElement _previousSibling;
        private ProjectElement _nextSibling;

        /// <summary>
        /// Constructor called by ProjectRootElement only.
        /// XmlElement is set directly after construction.
        /// </summary>
        /// <comment>
        /// Should be protected+internal.
        /// </comment>
        internal ProjectElement()
        {
        }

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectElement(ProjectElementLink link)
        {
            ErrorUtilities.VerifyThrowArgumentNull(link, nameof(link));

            _xmlSource = link;
        }

        /// <summary>
        /// Constructor called by derived classes, except from ProjectRootElement.
        /// Parameters may not be null, except parent.
        /// </summary>
        internal ProjectElement(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(xmlElement, nameof(xmlElement));
            ErrorUtilities.VerifyThrowArgumentNull(containingProject, nameof(containingProject));

            _xmlSource = (XmlElementWithLocation)xmlElement;
            _parent = parent;
            ContainingProject = containingProject;
        }

        /// <summary>
        /// Allows data (for example, item metadata) to be represented as an attribute on the parent element instead of as a child element.
        /// </summary>
        /// <remarks>
        /// If this is true, then the <see cref="XmlElement"/> will still be used to hold the data for this (pseudo) ProjectElement, but
        /// it will not be added to the Xml tree.  
        /// </remarks>
        internal virtual bool ExpressedAsAttribute
        {
            get => Link != null ? Link.ExpressedAsAttribute : _expressedAsAttribute;
            set
            {
                if (Link != null)
                {
                    Link.ExpressedAsAttribute = value;
                }
                else if (value != _expressedAsAttribute)
                {
                    Parent?.RemoveFromXml(this);
                    _expressedAsAttribute = value;
                    Parent?.AddToXml(this);
                    MarkDirty("Set express as attribute: {0}", value.ToString());
                }                
            }
        }

        /// <summary>
        /// Gets or sets the Condition value. 
        /// It will return empty string IFF a condition attribute is legal but it’s not present or has no value. 
        /// It will return null IFF a Condition attribute is illegal on that element.
        /// Removes the attribute if the value to set is empty.
        /// It is possible for derived classes to throw an <see cref="InvalidOperationException"/> if setting the condition is
        /// not applicable for those elements.
        /// </summary>
        /// <example> For the "ProjectExtensions" element, the getter returns null and the setter
        /// throws an exception for any value. </example>
        public virtual string Condition
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.condition, ref _condition);
            }

            [DebuggerStepThrough]
            set
            {
                SetOrRemoveAttribute(XMakeAttributes.condition, value, ref _condition, "Set condition {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Label value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string Label
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.label);
            }

            [DebuggerStepThrough]
            set
            {
                SetOrRemoveAttribute(XMakeAttributes.label, value, "Set label {0}", value);
            }
        }

        /// <summary>
        /// Null if this is a ProjectRootElement.
        /// Null if this has not been attached to a parent yet.
        /// </summary>
        /// <remarks>
        /// Parent should only be set by ProjectElementContainer.
        /// </remarks>
        public ProjectElementContainer Parent
        {
            [DebuggerStepThrough]
            get
            {
                if (this.Link != null) { return this.Link.Parent; }

                if (_parent is WrapperForProjectRootElement)
                {
                    // We hijacked the field to store the owning PRE. This element is actually unparented.
                    return null;
                }

                return _parent;
            }

            internal set
            {
                ErrorUtilities.VerifyThrow(Link == null, "External project");
                if (value == null)
                {
                    // We're about to lose the parent. Hijack the field to store the owning PRE.
                    _parent = new WrapperForProjectRootElement(ContainingProject);
                }
                else
                {
                    _parent = value;
                }

                OnAfterParentChanged(value);
            }
        }

        /// <inheritdoc/>
        public string OuterElement => Link != null ? Link.OuterElement : XmlElement.OuterXml;

        /// <summary>
        /// All parent elements of this element, going up to the ProjectRootElement.
        /// None if this itself is a ProjectRootElement.
        /// None if this itself has not been attached to a parent yet.
        /// </summary>
        public IEnumerable<ProjectElementContainer> AllParents
        {
            get
            {
                ProjectElementContainer currentParent = Parent;
                while (currentParent != null)
                {
                    yield return currentParent;
                    currentParent = currentParent.Parent;
                }
            }
        }

        /// <summary>
        /// Previous sibling element.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// Setter should ideally be "protected AND internal"
        /// </remarks>
        public ProjectElement PreviousSibling
        {
            [DebuggerStepThrough]
            get => Link != null? Link.PreviousSibling : _previousSibling;
            [DebuggerStepThrough]
            internal set => _previousSibling = value;
        }

        /// <summary>
        /// Next sibling element.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// Setter should ideally be "protected AND internal"
        /// </remarks>
        public ProjectElement NextSibling
        {
            [DebuggerStepThrough]
            get => Link != null ? Link.NextSibling : _nextSibling;
            [DebuggerStepThrough]
            internal set => _nextSibling = value;
        }

        /// <summary>
        /// ProjectRootElement (possibly imported) that contains this Xml.
        /// Cannot be null.
        /// </summary>
        /// <remarks>
        /// Setter ideally would be "protected and internal"
        /// There are some tricks here in order to save the space of a field: there are a lot of these objects.
        /// </remarks>
        public ProjectRootElement ContainingProject
        {
            get
            {
                if (Link != null)
                {
                    return Link.ContainingProject;
                }

                // If this element is unparented, we have hijacked the 'parent' field and stored the owning PRE in a special wrapper; get it from that.
                if (_parent is WrapperForProjectRootElement wrapper)
                {
                    return wrapper.ContainingProject;
                }

                // If this element is parented, the parent field is the true parent, and we ask that for the PRE.
                // It will call into this same getter on itself and figure it out.
                return Parent.ContainingProject;
            }

            // ContainingProject is set ONLY when an element is first constructed.
            internal set
            {
                ErrorUtilities.VerifyThrow(Link == null, "External project");
                ErrorUtilities.VerifyThrowArgumentNull(value, "ContainingProject");

                if (_parent == null)
                {
                    // Not parented yet, hijack the field to store the ContainingProject
                    _parent = new WrapperForProjectRootElement(value);
                }
            }
        }

        /// <summary>
        /// Location of the "Condition" attribute on this element, if any.
        /// If there is no such attribute, returns null.
        /// </summary>
        public virtual ElementLocation ConditionLocation => GetAttributeLocation(XMakeAttributes.condition);

        /// <summary>
        /// Location of the "Label" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation LabelLocation => GetAttributeLocation(XMakeAttributes.label);

        /// <summary>
        /// Location of the corresponding Xml element.
        /// May not be correct if file is not saved, or 
        /// file has been edited since it was last saved.
        /// In the case of an unsaved edit, the location only
        /// contains the path to the file that the element originates from.
        /// </summary>
        public ElementLocation Location => Link != null ? Link.Location :  XmlElement.Location;

        /// <inheritdoc/>
        public string ElementName => Link != null? Link.ElementName : XmlElement.Name;

        // Using ILinkedXml to share single field for either Linked (external) and local (XML backed) nodes.
        private ILinkedXml _xmlSource;

        internal ProjectElementLink Link => _xmlSource?.Link;

        /// <summary>
        /// <see cref="ILinkableObject.Link"/>
        /// </summary>
        object ILinkableObject.Link => Link;


        /// <summary>
        /// Gets the XmlElement associated with this project element.
        /// The setter is used when adding new elements.
        /// Never null except during load or creation.
        /// </summary>
        /// <remarks>
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal XmlElementWithLocation XmlElement => _xmlSource?.Xml;

        /// <summary>
        /// Gets the XmlDocument associated with this project element.
        /// </summary>
        /// <remarks>
        /// Never null except during load or creation.
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal XmlDocumentWithLocation XmlDocument
        {
            [DebuggerStepThrough]
            get
            {
                return (XmlDocumentWithLocation) XmlElement?.OwnerDocument;
            }
        }

        /// <summary>
        /// Returns a shallow clone of this project element.
        /// </summary>
        /// <returns>The cloned element.</returns>
        public ProjectElement Clone()
        {
            return Clone(ContainingProject);
        }

        /// <summary>
        /// Applies properties from the specified type to this instance.
        /// </summary>
        /// <param name="element">The element to act as a template to copy from.</param>
        public virtual void CopyFrom(ProjectElement element)
        {
            ErrorUtilities.VerifyThrowArgumentNull(element, nameof(element));
            ErrorUtilities.VerifyThrowArgument(GetType().IsEquivalentTo(element.GetType()), nameof(element));

            if (this == element)
            {
                return;
            }

            if (Link != null)
            {
                Link.CopyFrom(element);
                return;
            }

            // Remove all the current attributes and textual content.
            XmlElement.RemoveAllAttributes();
            if (XmlElement.ChildNodes.Count == 1 && XmlElement.FirstChild.NodeType == XmlNodeType.Text)
            {
                XmlElement.RemoveChild(XmlElement.FirstChild);
            }

            // Ensure the element name itself matches.
            ReplaceElement(XmlUtilities.RenameXmlElement(XmlElement, element.ElementName, XmlElement.NamespaceURI));

            // hard case when argument is a linked object (slight duplication).
            if (element.Link != null)
            {
                foreach (var remoteAttribute in element.Link.Attributes)
                {
                    if (ShouldCloneXmlAttribute(remoteAttribute))
                    {
                        XmlElement.SetAttribute(remoteAttribute.LocalName, remoteAttribute.NamespaceURI, remoteAttribute.Value);
                    }
                }
                var pureText = element.Link.PureText;
                if (pureText != null)
                {
                    XmlElement.AppendChild(XmlElement.OwnerDocument.CreateTextNode(pureText));
                }

                _expressedAsAttribute = element.ExpressedAsAttribute;
            }
            else
            {
                // Copy over the attributes from the template element.
                foreach (XmlAttribute attribute in element.XmlElement.Attributes)
                {
                    if (ShouldCloneXmlAttribute(attribute))
                    {
                        XmlElement.SetAttribute(attribute.LocalName, attribute.NamespaceURI, attribute.Value);
                    }
                }

                // If this element has pure text content, copy that over.
                if (element.XmlElement.ChildNodes.Count == 1 && element.XmlElement.FirstChild.NodeType == XmlNodeType.Text)
                {
                    XmlElement.AppendChild(XmlElement.OwnerDocument.CreateTextNode(element.XmlElement.FirstChild.Value));
                }

                _expressedAsAttribute = element._expressedAsAttribute;
            }

            MarkDirty("CopyFrom", null);
            ClearAttributeCache();
        }

        /// <summary>
        /// Hook for subclasses to specify whether the given <param name="attribute"></param> should be cloned or not
        /// </summary>
        protected virtual bool ShouldCloneXmlAttribute(XmlAttribute attribute) => true;

        internal virtual bool ShouldCloneXmlAttribute(XmlAttributeLink attributeLink) => true;

        /// <summary>
        /// Called only by the parser to tell the ProjectRootElement its backing XmlElement and its own parent project (itself)
        /// This can't be done during construction, as it hasn't loaded the document at that point and it doesn't have a 'this' pointer either.
        /// </summary>
        internal void SetProjectRootElementFromParser(XmlElementWithLocation xmlElement, ProjectRootElement projectRootElement)
        {
            _xmlSource = xmlElement;
            ContainingProject = projectRootElement;
        }

        /// <summary>
        /// Called by ProjectElementContainer to clear the parent when
        /// removing an element from its parent.
        /// </summary>
        internal void ClearParent()
        {
            Parent = null;
        }

        /// <summary>
        /// Called by a DERIVED CLASS to indicate its XmlElement has changed.
        /// This normally shouldn't happen, so it's broken out into an explicit method.
        /// An example of when it has to happen is when an item's type is changed.
        /// We trust the caller to have fixed up the XmlDocument properly.
        /// We ASSUME that attributes were copied verbatim. If this is not the case,
        /// any cached attribute values would have to be cleared.
        /// If the new element is actually the existing element, does nothing, and does
        /// not mark the project dirty.
        /// </summary>
        /// <remarks>
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal void ReplaceElement(XmlElementWithLocation newElement)
        {
            if (ReferenceEquals(newElement, XmlElement))
            {
                return;
            }

            _xmlSource = newElement;
            MarkDirty("Replace element {0}", newElement.Name);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal abstract void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer proposedParent, ProjectElement previousSibling, ProjectElement nextSibling);

        /// <summary>
        /// Marks this element as dirty.
        /// The default implementation simply marks the parent as dirty.
        /// If there is no parent, because the element has not been parented, do nothing. The parent
        /// will be dirtied when the element is added.
        /// Accepts a reason for debugging purposes only, and optional reason parameter.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal virtual void MarkDirty(string reason, string param)
        {
            Parent?.MarkDirty(reason, param);
        }

        /// <summary>
        /// Called after a new parent is set. Parent may be null.
        /// By default does nothing.
        /// </summary>
        internal virtual void OnAfterParentChanged(ProjectElementContainer newParent)
        {
        }

        /// <summary>
        /// Returns a shallow clone of this project element.
        /// </summary>
        /// <param name="factory">The factory to use for creating the new instance.</param>
        /// <returns>The cloned element.</returns>
        protected internal virtual ProjectElement Clone(ProjectRootElement factory)
        {
            var clone = CreateNewInstance(factory);
            if (!clone.GetType().IsEquivalentTo(GetType()))
            {
                ErrorUtilities.ThrowInternalError("{0}.Clone() returned an instance of type {1}.", GetType().Name, clone.GetType().Name);
            }

            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Returns a new instance of this same type.
        /// Any properties that cannot be set after creation should be set to copies of values
        /// as set for this instance.
        /// </summary>
        /// <param name="owner">The factory to use for creating the new instance.</param>
        protected abstract ProjectElement CreateNewInstance(ProjectRootElement owner);

        internal static ProjectElement CreateNewInstance(ProjectElement xml, ProjectRootElement owner)
        {
            if (xml.Link != null)
            {
                return xml.Link.CreateNewInstance(owner);
            }

            return xml.CreateNewInstance(owner);
        }

        internal ElementLocation GetAttributeLocation(string attributeName)
        {
            return Link != null ? Link.GetAttributeLocation(attributeName) : XmlElement.GetAttributeLocation(attributeName);
        }

        internal string GetAttributeValue(string attributeName, bool nullIfNotExists = false)
        {
            return Link != null ? Link.GetAttributeValue(attributeName, nullIfNotExists) :
                ProjectXmlUtilities.GetAttributeValue(XmlElement, attributeName, nullIfNotExists);
        }

        internal string GetAttributeValue(string attributeName, ref string cache)
        {
            if (cache != null) return cache;
            var value = GetAttributeValue(attributeName, false);
            if (Link == null)
            {
                cache = value;
            }

            return value;
        }

        internal virtual void ClearAttributeCache()
        {
            this._condition = null;
        }

        internal void SetOrRemoveAttributeForLink(string name, string value, bool clearAttributeCache, string reason, string param)
        {
            SetOrRemoveAttribute(name, value, reason, param);
            if (clearAttributeCache)
            {
                this.ClearAttributeCache();
            }
        }

        internal void SetOrRemoveAttribute(string name, string value, string reason = null, string param = null)
        {
            if (Link != null)
            {
                Link.SetOrRemoveAttribute(name, value, false, reason, param);
            }
            else
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, name, value);
                if (reason != null)
                {
                    MarkDirty(reason, param);
                }
            }
        }

        internal void SetOrRemoveAttribute(string name, string value, ref string cache, string reason = null, string param = null)
        {
            if (Link != null)
            {
                Link.SetOrRemoveAttribute(name, value, true, reason, param);
            }
            else
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, name, value);
                cache = value;
                if (reason != null)
                {
                    MarkDirty(reason, param);
                }
            }
        }


        /// <summary>
        /// Special derived variation of ProjectElementContainer used to wrap a ProjectRootElement.
        /// This is part of a trick used in ProjectElement to avoid using a separate field for the containing PRE.
        /// </summary>
        private class WrapperForProjectRootElement : ProjectElementContainer
        {
            /// <summary>
            /// Constructor
            /// </summary>
            internal WrapperForProjectRootElement(ProjectRootElement containingProject)
            {
                ErrorUtilities.VerifyThrowInternalNull(containingProject, nameof(containingProject));
                ContainingProject = containingProject;
            }

            /// <summary>
            /// Wrapped ProjectRootElement
            /// </summary>
            internal new ProjectRootElement ContainingProject { get; }

            /// <summary>
            /// Dummy required implementation
            /// </summary>
            internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            /// <inheritdoc />
            protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
            {
                return new WrapperForProjectRootElement(ContainingProject);
            }
        }
    }
}
