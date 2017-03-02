// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

 using System;
using System.Xml;
using System.Collections;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Class representing the Choose construct.  The Choose class holds the list
    /// of When blocks and the Otherwise block.  It also contains other data such
    /// as the XmlElement, parent project, etc.
    /// </summary>
    internal class Choose : IItemPropertyGrouping
    {
        #region Member Data
        private ArrayList whenClauseList = null;
        private When otherwiseClause = null;
        private When whenLastTaken = null;

        // If this is a persisted <Choose>, this boolean tells us whether
        // it came from the main project file, or an imported project file.
        private bool importedFromAnotherProject;

        // Maximum nesting level of <Choose> elements. No reasonable project needs more
        // than this.
        private const int maximumChooseNesting = 50;

        #endregion

        #region Constructors

        /// <summary>
        /// Empty constructor for the Choose object.  This really should only
        /// be used by unit tests.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        internal Choose
        (
        )
        {
            whenClauseList = new ArrayList();
        }

        /// <summary>
        /// Constructor for the Choose object.  Parses the contents of the Choose
        /// and sets up list of When blocks
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentProject"></param>
        /// <param name="parentGroupingCollection"></param>
        /// <param name="chooseElement"></param>
        /// <param name="importedFromAnotherProject"></param>
        /// <param name="nestingDepth">stack overflow guard</param>
        internal Choose
        (
            Project parentProject,
            GroupingCollection parentGroupingCollection,
            XmlElement chooseElement,
            bool importedFromAnotherProject,
            int nestingDepth
        )
        {
            whenClauseList = new ArrayList();

            error.VerifyThrow(chooseElement != null, "Need valid <Choose> element.");

            // Make sure this really is the <Choose> node.
            ProjectXmlUtilities.VerifyThrowElementName(chooseElement, XMakeElements.choose);

            // Stack overflow guard. The only way in the MSBuild file format that MSBuild elements can be
            // legitimately nested without limit is the <Choose> construct. So, enforce a nesting limit 
            // to avoid blowing our stack.
            nestingDepth++;
            ProjectErrorUtilities.VerifyThrowInvalidProject(nestingDepth <= maximumChooseNesting, chooseElement, "ChooseOverflow", maximumChooseNesting);

            this.importedFromAnotherProject = importedFromAnotherProject;

            // This <Choose> is coming from an existing XML element, so
            // walk through all the attributes and child elements, creating the
            // necessary When objects.

            // No attributes on the <Choose> element, so don't allow any.
            ProjectXmlUtilities.VerifyThrowProjectNoAttributes(chooseElement);

            bool foundOtherwise = false;
            // Loop through the child nodes of the <Choose> element.
            foreach (XmlNode chooseChildNode in chooseElement)
            {
                switch (chooseChildNode.NodeType)
                {
                    // Handle XML comments under the <PropertyGroup> node (just ignore them).
                    case XmlNodeType.Comment:
                    // fall through
                    case XmlNodeType.Whitespace:
                        // ignore whitespace
                        break;

                    case XmlNodeType.Element:
                        // The only two types of child nodes that a <Choose> element can contain
                        // is are <When> elements and zero or one <Otherwise> elements.

                        ProjectXmlUtilities.VerifyThrowProjectValidNamespace((XmlElement)chooseChildNode);

                        if (chooseChildNode.Name == XMakeElements.when)
                        {
                            // don't allow <When> to follow <Otherwise>
                            ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise,
                                    chooseChildNode, "WhenNotAllowedAfterOtherwise");
                            When newWhen = new When(parentProject,
                                parentGroupingCollection,
                                (XmlElement)chooseChildNode,
                                importedFromAnotherProject,
                                When.Options.ProcessWhen,
                                nestingDepth);
                            this.whenClauseList.Add(newWhen);
                        }
                        else if (chooseChildNode.Name == XMakeElements.otherwise)
                        {
                            ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise,
                                        chooseChildNode, "MultipleOtherwise");
                            When newWhen = new When(parentProject,
                                parentGroupingCollection,
                                (XmlElement)chooseChildNode,
                                importedFromAnotherProject,
                                When.Options.ProcessOtherwise,
                                nestingDepth);
                            otherwiseClause = newWhen;
                            foundOtherwise = true;
                        }
                        else
                        {
                            ProjectXmlUtilities.ThrowProjectInvalidChildElement(chooseChildNode);
                        }
                        break;

                    default:
                        // Unrecognized child element.
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(chooseChildNode);
                        break;
                }
            }
            ProjectErrorUtilities.VerifyThrowInvalidProject(this.whenClauseList.Count != 0,
                    chooseElement, "ChooseMustContainWhen");
        }
        #endregion

        #region Properties

        /// <summary>
        /// The list of When nodes inside this Choose
        /// </summary>
        internal ArrayList Whens
        {
            get
            {
                return whenClauseList;
            }
        }

        /// <summary>
        /// The Otherwise node inside this Choose. May be null.
        /// </summary>
        internal When Otherwise
        {
            get
            {
                return otherwiseClause;
            }
        }

        /// <summary>
        /// True if this Choose is located in an imported project.
        /// </summary>
        internal bool IsImported
        {
            get
            {
                return importedFromAnotherProject;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Evaluates the Choose clause by stepping through each when and evaluating.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentPropertyBag"></param>
        /// <param name="ignoreCondition"></param>
        /// <param name="honorCondition"></param>
        /// <param name="conditionedPropertiesTable"></param>
        /// <param name="pass"></param>
        internal void Evaluate
        (
            BuildPropertyGroup parentPropertyBag,
            bool ignoreCondition, bool honorCondition,
            Hashtable conditionedPropertiesTable,
            ProcessingPass pass
        )
        {
            if (pass == ProcessingPass.Pass1)
            {
                whenLastTaken = null;
                bool whenTaken = false;
                foreach (When currentWhen in this.whenClauseList)
                {
                    if (currentWhen.EvaluateCondition(parentPropertyBag, conditionedPropertiesTable))
                    {
                        whenTaken = true;
                        currentWhen.Evaluate(parentPropertyBag, ignoreCondition, honorCondition, conditionedPropertiesTable, pass);
                        whenLastTaken = currentWhen;
                        break;
                    }
                }
                if (!whenTaken && otherwiseClause != null)
                {
                    // Process otherwise
                    whenLastTaken = otherwiseClause;
                    otherwiseClause.Evaluate(parentPropertyBag, ignoreCondition, honorCondition, conditionedPropertiesTable, pass);
                }
            }
            else
            {
                ErrorUtilities.VerifyThrow(pass == ProcessingPass.Pass2, "ProcessingPass must be Pass1 or Pass2.");
                if (whenLastTaken != null)
                {
                    whenLastTaken.Evaluate(parentPropertyBag, ignoreCondition, honorCondition, conditionedPropertiesTable, pass);
                }
            }
        }

        #endregion
    }
}
