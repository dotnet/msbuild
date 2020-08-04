// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using System.IO;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;

using error = Microsoft.Build.BuildEngine.Shared.ErrorUtilities;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Class representing a When block (also used to represent the Otherwise
    /// block on a Choose).
    /// </summary>
    internal class When
    {
        #region Member Data

        public enum Options
        {
            ProcessWhen,
            ProcessOtherwise,
        };

        private GroupingCollection propertyAndItemLists = null;
        private Project parentProject = null;

        // This is the "Condition" attribute on the <PropertyGroup> element above.
        private XmlAttribute conditionAttribute = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the When block.  Parses the contents of the When block (property
        /// groups, item groups, and nested chooses) and stores them.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentProject"></param>
        /// <param name="parentGroupingCollection"></param>
        /// <param name="whenElement"></param>
        /// <param name="importedFromAnotherProject"></param>
        /// <param name="options"></param>
        /// <param name="nestingDepth">stack overflow guard</param>
        internal When(
            Project parentProject,
            GroupingCollection parentGroupingCollection,
            XmlElement whenElement,
            bool importedFromAnotherProject,
            Options options,
            int nestingDepth
            )
        {
            // Make sure the <When> node has been given to us.
            error.VerifyThrow(whenElement != null, "Need valid (non-null) <When> element.");

            // Make sure this really is the <When> node.
            error.VerifyThrow(whenElement.Name == XMakeElements.when || whenElement.Name == XMakeElements.otherwise,
                "Expected <{0}> or <{1}> element; received <{2}> element.",
                XMakeElements.when, XMakeElements.otherwise, whenElement.Name);

            this.propertyAndItemLists = new GroupingCollection(parentGroupingCollection);
            this.parentProject = parentProject;

            string elementName = ((options == Options.ProcessWhen) ? XMakeElements.when : XMakeElements.otherwise);

            if (options == Options.ProcessWhen)
            {
                conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(whenElement, /*verify sole attribute*/ true);
                ProjectErrorUtilities.VerifyThrowInvalidProject(conditionAttribute != null, whenElement, "MissingCondition", XMakeElements.when);            
            }
            else
            {
                ProjectXmlUtilities.VerifyThrowProjectNoAttributes(whenElement);
            }

            ProcessWhenChildren(whenElement, parentProject, importedFromAnotherProject, nestingDepth);
        }
        #endregion

        #region Properties

        /// <summary>
        /// Property containing the condition for the When clause.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>string</returns>
        internal string Condition
        {
            get
            {
                return (this.conditionAttribute == null) ? String.Empty : this.conditionAttribute.Value;
            }
        }

        /// <summary>
        /// Property containing the condition for the When clause.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <returns>string</returns>
        internal XmlAttribute ConditionAttribute
        {
            get
            {
                return this.conditionAttribute;
            }
        }
        #endregion

        /// <summary>
        /// The collection of all sub-groups (item/property groups and chooses) inside this When
        /// </summary>
        internal GroupingCollection PropertyAndItemLists
        {
            get
            {
                return this.propertyAndItemLists;
            }
        }

        #region Methods

        /// <summary>
        /// Helper method for processing the children of a When. Only parses Choose,
        /// PropertyGroup, and ItemGroup. All other tags result in an error.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentNode"></param>
        /// <param name="parentProjectForChildren"></param>
        /// <param name="importedFromAnotherProject"></param>
        /// <param name="options"></param>
        /// <param name="nestingDepth">Number of parent &lt;Choose&gt; elements this is nested inside</param>
        private void ProcessWhenChildren
            (
            XmlElement parentNode,
            Project parentProjectForChildren, bool importedFromAnotherProject,
            int nestingDepth
            )
        {
            // Loop through the child nodes of the <When> element.
            foreach (XmlNode whenChildNode in parentNode)
            {
                switch (whenChildNode.NodeType)
                {
                    // Handle XML comments under the <When> node (just ignore them).
                    case XmlNodeType.Comment:
                    // fall through
                    case XmlNodeType.Whitespace:
                        // ignore whitespace
                        break;

                    case XmlNodeType.Element:
                        {
                            // Make sure this element doesn't have a custom namespace
                            ProjectXmlUtilities.VerifyThrowProjectValidNamespace((XmlElement)whenChildNode);

                            // The only three types of child nodes that a <When> element can contain
                            // are <PropertyGroup>, <ItemGroup> and <Choose>.
                            switch (whenChildNode.Name)
                            {
                                case XMakeElements.itemGroup:
                                    BuildItemGroup newItemGroup = new BuildItemGroup((XmlElement)whenChildNode, importedFromAnotherProject, parentProjectForChildren);
                                    this.propertyAndItemLists.InsertAtEnd(newItemGroup);
                                    break;

                                // Process the <PropertyGroup> element.
                                case XMakeElements.propertyGroup:
                                    BuildPropertyGroup newPropertyGroup = new BuildPropertyGroup(parentProjectForChildren, (XmlElement)whenChildNode, importedFromAnotherProject);
                                    newPropertyGroup.EnsureNoReservedProperties();
                                    this.propertyAndItemLists.InsertAtEnd(newPropertyGroup);
                                    break;

                                // Process the <Choose> element.
                                case XMakeElements.choose:
                                    Choose newChoose = new Choose(parentProjectForChildren, this.PropertyAndItemLists, (XmlElement)whenChildNode,
                                                                    importedFromAnotherProject, nestingDepth);
                                    this.propertyAndItemLists.InsertAtEnd(newChoose);
                                    break;

                                default:
                                    {
                                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(whenChildNode);
                                        break;
                                    }
                            }
                        }
                        break;

                    default:
                        {
                            ProjectXmlUtilities.ThrowProjectInvalidChildElement(whenChildNode);
                            break;
                        }
                }
            }
        }

       /// <summary>
        /// Evaluates a When clause.  Checks if the condition is true, and if it is,
        /// applies all of the contained property group, item lists, and import statements.
        /// Returns true if the When clause is process (because the condition is true), false
        /// otherwise.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentPropertyBag"></param>
        /// <param name="conditionedPropertiesTable"></param>
        /// <returns>bool</returns>
        internal bool EvaluateCondition
        (
            BuildPropertyGroup parentPropertyBag,
            Hashtable conditionedPropertiesTable
        )
        {
            if  (
                    (this.Condition != null) 
                    && 
                    !Utilities.EvaluateCondition(this.Condition, this.ConditionAttribute,
                        new Expander(parentPropertyBag, parentProject.EvaluatedItemsByName),
                        conditionedPropertiesTable, ParserOptions.AllowProperties, this.parentProject.ParentEngine.LoggingServices, this.parentProject.ProjectBuildEventContext)
                )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates a When clause.  Checks if the condition is true, and if it is,
        /// applies all of the contained property group, item lists, and import statements.
        /// Returns true if the When clause is process (because the condition is true), false
        /// otherwise.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <owner>DavidLe</owner>
        /// <param name="parentPropertyBag"></param>
        /// <param name="ignoreCondition"></param>
        /// <param name="honorCondition"></param>
        /// <param name="conditionedPropertiesTable"></param>
        /// <param name="pass"></param>
        /// <returns>bool</returns>
        internal void Evaluate
        (
            BuildPropertyGroup parentPropertyBag,
            bool ignoreCondition, bool honorCondition,
            Hashtable conditionedPropertiesTable,
            ProcessingPass pass
        )
        {
            foreach (IItemPropertyGrouping propOrItem in this.propertyAndItemLists)
            {
                // This is where we selectively evaluate PropertyGroups or Itemgroups during their respective passes.
                // Once we go to a one-pass model, we'll simple spin through all the children and evaluate.
                if (propOrItem is BuildPropertyGroup &&
                    pass == ProcessingPass.Pass1)
                {
                    ((BuildPropertyGroup) propOrItem).Evaluate(parentPropertyBag, conditionedPropertiesTable, pass);
                }
                else if (propOrItem is BuildItemGroup &&
                    pass == ProcessingPass.Pass2)
                {
                    ((BuildItemGroup) propOrItem).Evaluate(parentPropertyBag, parentProject.EvaluatedItemsByName, ignoreCondition, honorCondition, pass);
                }
                else if (propOrItem is Choose)
                {
                    ((Choose) propOrItem).Evaluate(parentPropertyBag, ignoreCondition, honorCondition, conditionedPropertiesTable, pass);
                }
            }
        }

        #endregion
    }
}
