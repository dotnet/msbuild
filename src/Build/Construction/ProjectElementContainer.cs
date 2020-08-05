// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// A container for project elements
    /// </summary>
    public abstract class ProjectElementContainer : ProjectElement
    {
        private const string DEFAULT_INDENT = "  ";

        private int _count;
        private ProjectElement _firstChild;
        private ProjectElement _lastChild;

        internal ProjectElementContainerLink ContainerLink => (ProjectElementContainerLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectElementContainer(ProjectElementContainerLink link)
            :base(link)
        {
        }

        /// <summary>
        /// Constructor called by ProjectRootElement only.
        /// XmlElement is set directly after construction.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment> 
        internal ProjectElementContainer()
        {
        }

        /// <summary>
        /// Constructor called by derived classes, except from ProjectRootElement.
        /// Parameters may not be null, except parent.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal ProjectElementContainer(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
        }

        /// <summary>
        /// Get an enumerator over all children, gotten recursively.
        /// Walks the children in a depth-first manner.
        /// </summary>
        public IEnumerable<ProjectElement> AllChildren => GetChildrenRecursively();

        /// <summary>
        /// Get enumerable over all the children
        /// </summary>
        public ICollection<ProjectElement> Children
        {
            [DebuggerStepThrough]
            get
            {
                return new Collections.ReadOnlyCollection<ProjectElement>
                    (
                        new ProjectElementSiblingEnumerable(FirstChild)
                    );
            }
        }

        /// <summary>
        /// Get enumerable over all the children, starting from the last
        /// </summary>
        public ICollection<ProjectElement> ChildrenReversed
        {
            [DebuggerStepThrough]
            get
            {
                return new Collections.ReadOnlyCollection<ProjectElement>
                    (
                        new ProjectElementSiblingEnumerable(LastChild, false /* reverse */)
                    );
            }
        }

        /// <summary>
        /// Number of children of any kind
        /// </summary>
        public int Count { get => Link != null ? ContainerLink.Count : _count ; private set => _count = value; }

        /// <summary>
        /// First child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="PrependChild">PrependChild()</see>.
        /// </summary>
        public ProjectElement FirstChild { get => Link != null ? ContainerLink.FirstChild : _firstChild; private set => _firstChild = value; }

        /// <summary>
        /// Last child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="AppendChild">AppendChild()</see>.
        /// </summary>
        public ProjectElement LastChild { get => Link != null ? ContainerLink.LastChild : _lastChild; private set => _lastChild = value; }

        /// <summary>
        /// Insert the child after the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="PrependChild">PrependChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertAfterChild.
        /// </remarks>
        public void InsertAfterChild(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, nameof(child));
            if (Link != null)
            {
                ContainerLink.InsertAfterChild(child, reference);
                return;
            }

            if (reference == null)
            {
                PrependChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference, reference.NextSibling);

            child.Parent = this;

            if (LastChild == reference)
            {
                LastChild = child;
            }

            child.PreviousSibling = reference;
            child.NextSibling = reference.NextSibling;

            reference.NextSibling = child;

            if (child.NextSibling != null)
            {
                ErrorUtilities.VerifyThrow(child.NextSibling.PreviousSibling == reference, "Invalid structure");
                child.NextSibling.PreviousSibling = child;
            }

            AddToXml(child);

            Count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Insert the child before the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="AppendChild">AppendChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertBeforeChild.
        /// </remarks>
        public void InsertBeforeChild(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, nameof(child));

            if (Link != null)
            {
                ContainerLink.InsertBeforeChild(child, reference);
                return;
            }

            if (reference == null)
            {
                AppendChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference.PreviousSibling, reference);

            child.Parent = this;

            if (FirstChild == reference)
            {
                FirstChild = child;
            }

            child.PreviousSibling = reference.PreviousSibling;
            child.NextSibling = reference;

            reference.PreviousSibling = child;

            if (child.PreviousSibling != null)
            {
                ErrorUtilities.VerifyThrow(child.PreviousSibling.NextSibling == reference, "Invalid structure");
                child.PreviousSibling.NextSibling = child;
            }

            AddToXml(child);

            Count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Inserts the provided element as the last child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void AppendChild(ProjectElement child)
        {
            if (LastChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                ErrorUtilities.VerifyThrow(FirstChild != null, "Invalid structure");
                InsertAfterChild(child, LastChild);
            }
        }

        /// <summary>
        /// Inserts the provided element as the first child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void PrependChild(ProjectElement child)
        {
            if (FirstChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                ErrorUtilities.VerifyThrow(LastChild != null, "Invalid structure");
                InsertBeforeChild(child, FirstChild);
            }
        }

        /// <summary>
        /// Removes the specified child.
        /// Throws if the child is not currently parented by this object.
        /// This is O(1).
        /// May be safely called during enumeration of the children.
        /// </summary>
        /// <remarks>
        /// This is actually safe to call during enumeration of children, because it
        /// doesn't bother to clear the child's NextSibling (or PreviousSibling) pointers.
        /// To determine whether a child is unattached, check whether its parent is null,
        /// or whether its NextSibling and PreviousSibling point back at it.
        /// DO NOT BREAK THIS VERY USEFUL SAFETY CONTRACT.
        /// </remarks>
        public void RemoveChild(ProjectElement child)
        {
            ErrorUtilities.VerifyThrowArgumentNull(child, nameof(child));

            ErrorUtilities.VerifyThrowArgument(child.Parent == this, "OM_NodeNotAlreadyParentedByThis");

            if (Link != null)
            {
                ContainerLink.RemoveChild(child);
                return;
            }

            child.ClearParent();

            if (child.PreviousSibling != null)
            {
                child.PreviousSibling.NextSibling = child.NextSibling;
            }

            if (child.NextSibling != null)
            {
                child.NextSibling.PreviousSibling = child.PreviousSibling;
            }

            if (ReferenceEquals(child, FirstChild))
            {
                FirstChild = child.NextSibling;
            }

            if (ReferenceEquals(child, LastChild))
            {
                LastChild = child.PreviousSibling;
            }

            RemoveFromXml(child);

            Count--;
            MarkDirty("Remove element {0}", child.ElementName);
        }

        /// <summary>
        /// Remove all the children, if any.
        /// </summary>
        /// <remarks>
        /// It is safe to modify the children in this way
        /// during enumeration. See <cref see="RemoveChild">RemoveChild</cref>.
        /// </remarks>
        public void RemoveAllChildren()
        {
            foreach (ProjectElement child in Children)
            {
                RemoveChild(child);
            }
        }

        /// <summary>
        /// Applies properties from the specified type to this instance.
        /// </summary>
        /// <param name="element">The element to act as a template to copy from.</param>
        public virtual void DeepCopyFrom(ProjectElementContainer element)
        {
            ErrorUtilities.VerifyThrowArgumentNull(element, nameof(element));
            ErrorUtilities.VerifyThrowArgument(GetType().IsEquivalentTo(element.GetType()), nameof(element));

            if (this == element)
            {
                return;
            }

            RemoveAllChildren();
            CopyFrom(element);

            foreach (ProjectElement child in element.Children)
            {
                if (child is ProjectElementContainer childContainer)
                {
                    childContainer.DeepClone(ContainingProject, this);
                }
                else
                {
                    AppendChild(child.Clone(ContainingProject));
                }
            }
        }

        /// <summary>
        /// Appends the provided child.
        /// Does not dirty the project, does not add an element, does not set the child's parent,
        /// and does not check the parent's future siblings and parent are acceptable.
        /// Called during project load, when the child can be expected to 
        /// already have a parent and its element is already connected to the
        /// parent's element.
        /// All that remains is to set FirstChild/LastChild and fix up the linked list.
        /// </summary>
        internal void AppendParentedChildNoChecks(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(child.Parent == this, "Expected parent already set");
            ErrorUtilities.VerifyThrow(child.PreviousSibling == null && child.NextSibling == null, "Invalid structure");
            ErrorUtilities.VerifyThrow(Link == null, "External project");

            if (LastChild == null)
            {
                FirstChild = child;
            }
            else
            {
                child.PreviousSibling = LastChild;
                LastChild.NextSibling = child;
            }

            LastChild = child;

            Count++;
        }

        /// <summary>
        /// Returns a clone of this project element and all its children.
        /// </summary>
        /// <param name="factory">The factory to use for creating the new instance.</param>
        /// <param name="parent">The parent to append the cloned element to as a child.</param>
        /// <returns>The cloned element.</returns>
        protected internal virtual ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent)
        {
            var clone = (ProjectElementContainer)Clone(factory);
            parent?.AppendChild(clone);

            foreach (ProjectElement child in Children)
            {
                if (child is ProjectElementContainer childContainer)
                {
                    childContainer.DeepClone(clone.ContainingProject, clone);
                }
                else
                {
                    clone.AppendChild(child.Clone(clone.ContainingProject));
                }
            }

            return clone;
        }

        internal static ProjectElementContainer DeepClone(ProjectElementContainer xml, ProjectRootElement factory, ProjectElementContainer parent)
        {
            return xml.DeepClone(factory, parent);
        }

        private void SetElementAsAttributeValue(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(Link == null, "External project");

            //  Assumes that child.ExpressedAsAttribute is true
            Debug.Assert(child.ExpressedAsAttribute, nameof(SetElementAsAttributeValue) + " method requires that " +
                nameof(child.ExpressedAsAttribute) + " property of child is true");

            string value = Internal.Utilities.GetXmlNodeInnerContents(child.XmlElement);
            ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, child.XmlElement.Name, value);
        }

        /// <summary>
        /// If child "element" is actually represented as an attribute, update the value in the corresponding Xml attribute
        /// </summary>
        /// <param name="child">A child element which might be represented as an attribute</param>
        internal void UpdateElementValue(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(Link == null, "External project");

            if (child.ExpressedAsAttribute)
            {
                SetElementAsAttributeValue(child);
            }
        }

        /// <summary>
        /// Adds a ProjectElement to the Xml tree
        /// </summary>
        /// <param name="child">A child to add to the Xml tree, which has already been added to the ProjectElement tree</param>
        /// <remarks>
        /// The MSBuild construction APIs keep a tree of ProjectElements and a parallel Xml tree which consists of
        /// objects from System.Xml.  This is a helper method which adds an XmlElement or Xml attribute to the Xml
        /// tree after the corresponding ProjectElement has been added to the construction API tree, and fixes up
        /// whitespace as necessary.
        /// </remarks>
        internal void AddToXml(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(Link == null, "External project");

            if (child.ExpressedAsAttribute)
            {
                // todo children represented as attributes need to be placed in order too
                //  Assume that the name of the child has already been validated to conform with rules in XmlUtilities.VerifyThrowArgumentValidElementName

                //  Make sure we're not trying to add multiple attributes with the same name
                ProjectErrorUtilities.VerifyThrowInvalidProject(!XmlElement.HasAttribute(child.XmlElement.Name),
                    XmlElement.Location, "InvalidChildElementDueToDuplication", child.XmlElement.Name, ElementName);

                SetElementAsAttributeValue(child);
            }
            else
            {
                //  We want to add the XmlElement to the same position in the child list as the corresponding ProjectElement.
                //  Depending on whether the child ProjectElement has a PreviousSibling or a NextSibling, we may need to
                //  use the InsertAfter, InsertBefore, or AppendChild methods to add it in the right place.
                //
                //  Also, if PreserveWhitespace is true, then the elements we add won't automatically get indented, so
                //  we try to match the surrounding formatting.

                // Siblings, in either direction in the linked list, may be represented either as attributes or as elements.
                // Therefore, we need to traverse both directions to find the first sibling of the same type as the one being added.
                // If none is found, then the node being added is inserted as the only node of its kind

                bool SiblingIsExplicitElement(ProjectElement _) => !_.ExpressedAsAttribute;

                if (TrySearchLeftSiblings(child.PreviousSibling, SiblingIsExplicitElement, out ProjectElement referenceSibling))
                {
                    //  Add after previous sibling
                    XmlElement.InsertAfter(child.XmlElement, referenceSibling.XmlElement);
                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  Try to match the surrounding formatting by checking the whitespace that precedes the node we inserted
                        //  after, and inserting the same whitespace between the previous node and the one we added
                        if (referenceSibling.XmlElement.PreviousSibling?.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(referenceSibling.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertAfter(newWhitespaceNode, referenceSibling.XmlElement);
                        }
                    }
                }
                else if (TrySearchRightSiblings(child.NextSibling, SiblingIsExplicitElement, out referenceSibling))
                {
                    //  Add as first child
                    XmlElement.InsertBefore(child.XmlElement, referenceSibling.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  Try to match the surrounding formatting by checking the whitespace that precedes where we inserted
                        //  the new node, and inserting the same whitespace between the node we added and the one after it.
                        if (child.XmlElement.PreviousSibling?.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(child.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertBefore(newWhitespaceNode, referenceSibling.XmlElement);
                        }
                    }
                }
                else
                {
                    //  Add as only child
                    XmlElement.AppendChild(child.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        //  If the empty parent has whitespace in it, delete it
                        if (XmlElement.FirstChild.NodeType == XmlNodeType.Whitespace)
                        {
                            XmlElement.RemoveChild(XmlElement.FirstChild);
                        }

                        var parentIndentation = GetElementIndentation(XmlElement);

                        var leadingWhitespaceNode = XmlDocument.CreateWhitespace(Environment.NewLine + parentIndentation + DEFAULT_INDENT);
                        var trailingWhiteSpaceNode = XmlDocument.CreateWhitespace(Environment.NewLine + parentIndentation);

                        XmlElement.InsertBefore(leadingWhitespaceNode, child.XmlElement);
                        XmlElement.InsertAfter(trailingWhiteSpaceNode, child.XmlElement);
                    }
                }
            }
        }

        private static string GetElementIndentation(XmlElementWithLocation xmlElement)
        {
            if (xmlElement.PreviousSibling?.NodeType != XmlNodeType.Whitespace)
            {
                return string.Empty;
            }

            var leadingWhiteSpace = xmlElement.PreviousSibling.Value;

            var lastIndexOfNewLine = leadingWhiteSpace.LastIndexOf("\n", StringComparison.Ordinal);

            if (lastIndexOfNewLine == -1)
            {
                return string.Empty;
            }

            // the last newline is not included in the indentation, only what comes after it
            return leadingWhiteSpace.Substring(lastIndexOfNewLine + 1);
        }

        internal void RemoveFromXml(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(Link == null, "External project");

            if (child.ExpressedAsAttribute)
            {
                XmlElement.RemoveAttribute(child.XmlElement.Name);
            }
            else
            {
                var previousSibling = child.XmlElement.PreviousSibling;

                XmlElement.RemoveChild(child.XmlElement);

                if (XmlDocument.PreserveWhitespace)
                {
                    //  If we are trying to preserve formatting of the file, then also remove any whitespace
                    //  that came before the node we removed.
                    if (previousSibling?.NodeType == XmlNodeType.Whitespace)
                    {
                        XmlElement.RemoveChild(previousSibling);
                    }

                    //  If we removed the last non-whitespace child node, set IsEmpty to true so that we get:
                    //      <ItemName />
                    //  instead of:
                    //      <ItemName>
                    //      </ItemName>
                    if (XmlElement.HasChildNodes)
                    {
                        if (XmlElement.ChildNodes.Cast<XmlNode>().All(c => c.NodeType == XmlNodeType.Whitespace))
                        {
                            XmlElement.IsEmpty = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the first child in this container
        /// </summary>
        internal void AddInitialChild(ProjectElement child)
        {
            ErrorUtilities.VerifyThrow(FirstChild == null && LastChild == null, "Expecting no children");

            if (Link != null)
            {
                ContainerLink.AddInitialChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, null);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, null, null);

            child.Parent = this;

            FirstChild = child;
            LastChild = child;

            child.PreviousSibling = null;
            child.NextSibling = null;

            AddToXml(child);

            Count++;

            MarkDirty("Add child element named '{0}'", child.ElementName);
        }

        /// <summary>
        /// Common verification for insertion of an element.
        /// Reference may be null.
        /// </summary>
        private void VerifyForInsertBeforeAfterFirst(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(Parent != null || ContainingProject == this, "OM_ParentNotParented");
            ErrorUtilities.VerifyThrowInvalidOperation(reference == null || reference.Parent == this, "OM_ReferenceDoesNotHaveThisParent");
            ErrorUtilities.VerifyThrowInvalidOperation(child.Parent == null, "OM_NodeAlreadyParented");
            ErrorUtilities.VerifyThrowInvalidOperation(child.ContainingProject == ContainingProject, "OM_MustBeSameProject");

            // In RemoveChild() we do not update the victim's NextSibling (or PreviousSibling) to null, to allow RemoveChild to be
            // called within an enumeration. So we can't expect these to be null if the child was previously removed. However, we
            // can expect that what they point to no longer point back to it. They've been reconnected.
            ErrorUtilities.VerifyThrow(child.NextSibling == null || child.NextSibling.PreviousSibling != this, "Invalid structure");
            ErrorUtilities.VerifyThrow(child.PreviousSibling == null || child.PreviousSibling.NextSibling != this, "Invalid structure");
            VerifyThrowInvalidOperationNotSelfAncestor(child);
        }

        /// <summary>
        /// Verifies that the provided element isn't this element or a parent of it.
        /// If it is, throws InvalidOperationException.
        /// </summary>
        private void VerifyThrowInvalidOperationNotSelfAncestor(ProjectElement element)
        {
            ProjectElement ancestor = this;

            while (ancestor != null)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(ancestor != element, "OM_SelfAncestor");
                ancestor = ancestor.Parent;
            }
        }

        /// <summary>
        /// Recurses into the provided container (such as a choose) and finds all child elements, even if nested.
        /// Result does NOT include the element passed in.
        /// The caller could filter these.
        /// </summary>
        private IEnumerable<ProjectElement> GetChildrenRecursively()
        {
            ProjectElement child = FirstChild;

            while (child != null)
            {
                yield return child;

                if (child is ProjectElementContainer container)
                {
                    foreach (ProjectElement grandchild in container.AllChildren)
                    {
                        yield return grandchild;
                    }
                }

                child = child.NextSibling;
            }
        }

        private static bool TrySearchLeftSiblings(ProjectElement initialElement, Predicate<ProjectElement> siblingIsAcceptable, out ProjectElement referenceSibling)
        {
            return TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.PreviousSibling, out referenceSibling);
        }

        private static bool TrySearchRightSiblings(ProjectElement initialElement, Predicate<ProjectElement> siblingIsAcceptable, out ProjectElement referenceSibling)
        {
            return TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.NextSibling, out referenceSibling);
        }

        private static bool TrySearchSiblings(
            ProjectElement initialElement,
            Predicate<ProjectElement> siblingIsAcceptable,
            Func<ProjectElement, ProjectElement> nextSibling,
            out ProjectElement referenceSibling)
        {
            if (initialElement == null)
            {
                referenceSibling = null;
                return false;
            }

            var sibling = initialElement;

            while (sibling != null && !siblingIsAcceptable(sibling))
            {
                sibling = nextSibling(sibling);
            }

            referenceSibling = sibling;

            return referenceSibling != null;
        }

        /// <summary>
        /// Enumerable over a series of sibling ProjectElement objects
        /// </summary>
        private struct ProjectElementSiblingEnumerable : IEnumerable<ProjectElement>
        {
            /// <summary>
            /// The enumerator
            /// </summary>
            private readonly ProjectElementSiblingEnumerator _enumerator;

            /// <summary>
            /// Constructor allowing reverse enumeration
            /// </summary>
            internal ProjectElementSiblingEnumerable(ProjectElement initial, bool forwards = true)
            {
                _enumerator = new ProjectElementSiblingEnumerator(initial, forwards);
            }

            /// <summary>
            /// Get enumerator
            /// </summary>
            public IEnumerator<ProjectElement> GetEnumerator()
            {
                return _enumerator;
            }

            /// <summary>
            /// Get non generic enumerator
            /// </summary>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _enumerator;
            }

            /// <summary>
            /// Enumerator over a series of sibling ProjectElement objects
            /// </summary>
            private struct ProjectElementSiblingEnumerator : IEnumerator<ProjectElement>
            {
                /// <summary>
                /// First element
                /// </summary>
                private readonly ProjectElement _initial;

                /// <summary>
                /// Whether enumeration should go forwards or backwards.
                /// If backwards, the "initial" will be the first returned, then each previous
                /// node in turn.
                /// </summary>
                private readonly bool _forwards;

                /// <summary>
                /// Constructor taking the first element
                /// </summary>
                internal ProjectElementSiblingEnumerator(ProjectElement initial, bool forwards)
                {
                    _initial = initial;
                    Current = null;
                    _forwards = forwards;
                }

                /// <summary>
                /// Current element
                /// Returns null if MoveNext() hasn't been called
                /// </summary>
                public ProjectElement Current { get; private set; }

                /// <summary>
                /// Current element.
                /// Throws if MoveNext() hasn't been called
                /// </summary>
                object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (Current != null)
                        {
                            return Current;
                        }

                        throw new InvalidOperationException();
                    }
                }

                /// <summary>
                /// Dispose. Do nothing.
                /// </summary>
                public void Dispose()
                {
                }

                /// <summary>
                /// Moves to the next item if any, otherwise returns false
                /// </summary>
                public bool MoveNext()
                {
                    ProjectElement next;

                    if (Current == null)
                    {
                        next = _initial;
                    }
                    else
                    {
                        next = _forwards ? Current.NextSibling : Current.PreviousSibling;
                    }

                    if (next != null)
                    {
                        Current = next;
                        return true;
                    }

                    return false;
                }

                /// <summary>
                /// Return to start
                /// </summary>
                public void Reset()
                {
                    Current = null;
                }
            }
        }
    }
}
