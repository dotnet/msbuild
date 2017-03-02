// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Build.BuildEngine.Shared;
using MetadataDictionary = System.Collections.Generic.Dictionary<string, string>;
using ItemDefinitionsDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;
using System.Collections;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A library of default metadata values by item type.
    /// Projects each have exactly one of these.
    /// BuildItems consult the appropriate library to check
    /// for default metadata values when they do not have an
    /// explicit value set.
    /// </summary>
    internal class ItemDefinitionLibrary
    {
        #region Fields

        Project parentProject;
        List<ItemDefinitionLibrary.BuildItemDefinitionGroupXml> itemDefinitions;
        ItemDefinitionsDictionary itemDefinitionsDictionary;
        bool evaluated;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new item definition library.
        /// The project is required only to give error context.
        /// </summary>
        internal ItemDefinitionLibrary(Project parentProject)
        {
            this.parentProject = parentProject;
            this.itemDefinitions = new List<BuildItemDefinitionGroupXml>();
            this.itemDefinitionsDictionary = new ItemDefinitionsDictionary(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Properties

        internal bool IsEvaluated
        {
            get { return evaluated; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Create a BuildItemDefinitionGroupXml element and add it to the end of our ordered list.
        /// </summary>
        /// <exception cref="InvalidProjectFileException">If element does not represent a valid ItemDefinitionGroup element</exception>
        internal void Add(XmlElement element)
        {
            BuildItemDefinitionGroupXml itemDefinitionGroupXml = new BuildItemDefinitionGroupXml(element, parentProject);
            itemDefinitions.Add(itemDefinitionGroupXml);

            evaluated = false;
        }

        /// <summary>
        /// Go through each &lt;BuildItemDefinition&gt; element in order, evaluating it using the
        /// supplied properties and any previously evaluated definitions, to build up a complete
        /// library of item types and their default metadata values.
        /// </summary>
        internal void Evaluate(BuildPropertyGroup evaluatedProperties)
        {
            // Clear out previous data first
            itemDefinitionsDictionary.Clear();

            foreach (BuildItemDefinitionGroupXml itemDefinitionGroupXml in itemDefinitions)
            {
                itemDefinitionGroupXml.Evaluate(evaluatedProperties, itemDefinitionsDictionary);
            }

            evaluated = true;
        }

        /// <summary>
        /// Returns any default metadata value for the specified item type and metadata name.
        /// If no default exists, returns null.
        /// </summary>
        internal string GetDefaultMetadataValue(string itemType, string metadataName)
        {
            MustBeEvaluated();

            string value = null;

            MetadataDictionary metadataDictionary;
            if (itemDefinitionsDictionary.TryGetValue(itemType, out metadataDictionary))
            {
                metadataDictionary.TryGetValue(metadataName, out value);
            }

            return value;
        }

        /// <summary>
        /// Count of default metadata for the specified item type
        /// </summary>
        internal int GetDefaultedMetadataCount(string itemType)
        {
            MustBeEvaluated();

            MetadataDictionary metadataDictionary;
            if (itemDefinitionsDictionary.TryGetValue(itemType, out metadataDictionary))
            {
                return metadataDictionary.Count;
            }

            return 0;
        }

        /// <summary>
        /// Names of metadata that have defaults for the specified item type.
        /// Null if there are none.
        /// </summary>
        internal ICollection<string> GetDefaultedMetadataNames(string itemType)
        {
            MustBeEvaluated();

            MetadataDictionary metadataDictionary = GetDefaultedMetadata(itemType);
            if (metadataDictionary != null)
            {
                return metadataDictionary.Keys;
            }

            return null;
        }

        /// <summary>
        /// All default metadata names and values for the specified item type.
        /// Null if there are none.
        /// </summary>
        internal MetadataDictionary GetDefaultedMetadata(string itemType)
        {
            MustBeEvaluated();

            MetadataDictionary metadataDictionary;
            if (itemDefinitionsDictionary.TryGetValue(itemType, out metadataDictionary))
            {
                return metadataDictionary;
            }

            return null;
        }

        /// <summary>
        /// Verify this library has already been evaluated
        /// </summary>
        private void MustBeEvaluated()
        {
            ErrorUtilities.VerifyThrowNoAssert(evaluated, "Must be evaluated to query");
        }

        #endregion

        /// <summary>
        /// Encapsulates an &lt;ItemDefinitionGroup&gt; tag.
        /// </summary>
        /// <remarks>
        /// Only used by ItemDefinitionLibrary -- private and nested inside it as no other class should know about this.
        /// Since at present this has no OM or editing support, and is not passed around,
        /// there are currently no separate classes for the child tags, and no separate BuildItemDefinitionGroup class. 
        /// They can be broken out in future if necessary.
        /// </remarks>
        private class BuildItemDefinitionGroupXml
        {
            #region Fields

            XmlElement element;
            Project parentProject;
            XmlAttribute conditionAttribute;
            string condition;

            #endregion

            #region Constructors

            /// <summary>
            /// Read in and validate an &lt;ItemDefinitionGroup&gt; element and all its children.
            /// This is currently only called from ItemDefinitionLibrary. Projects don't know about it.
            /// </summary>
            internal BuildItemDefinitionGroupXml(XmlElement element, Project parentProject)
            {
                ProjectXmlUtilities.VerifyThrowElementName(element, XMakeElements.itemDefinitionGroup);
                ProjectXmlUtilities.VerifyThrowProjectValidNamespace(element);

                this.element = element;
                this.parentProject = parentProject;
                this.conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(element, /* sole attribute */ true);
                this.condition = ProjectXmlUtilities.GetAttributeValue(conditionAttribute);

                // Currently, don't bother validating the children until evaluation time
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Given the properties and dictionary of previously encountered item definitions, evaluates 
            /// this group of item definitions and adds to the dictionary as necessary.
            /// </summary>
            /// <exception cref="InvalidProjectFileException">If the item definitions are incorrectly defined</exception>
            internal void Evaluate(BuildPropertyGroup properties, ItemDefinitionsDictionary itemDefinitionsDictionary)
            {
                Expander expander = new Expander(properties);

                if (!Utilities.EvaluateCondition(condition, conditionAttribute, expander, ParserOptions.AllowProperties, parentProject))
                {
                    return;
                }

                List<XmlElement> childElements = ProjectXmlUtilities.GetValidChildElements(element);

                foreach (XmlElement child in childElements)
                {
                    EvaluateItemDefinitionElement(child, properties, itemDefinitionsDictionary);
                }
            }

            /// <summary>
            /// Given the properties and dictionary of previously encountered item definitions, evaluates 
            /// this specific item definition element and adds to the dictionary as necessary.
            /// </summary>
            /// <exception cref="InvalidProjectFileException">If the item definition is incorrectly defined</exception>
            private void EvaluateItemDefinitionElement(XmlElement itemDefinitionElement, BuildPropertyGroup properties, ItemDefinitionsDictionary itemDefinitionsDictionary)
            {
                ProjectXmlUtilities.VerifyThrowProjectValidNameAndNamespace(itemDefinitionElement);

                XmlAttribute conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(itemDefinitionElement, /* sole attribute */ true);
                string condition = ProjectXmlUtilities.GetAttributeValue(conditionAttribute);

                MetadataDictionary metadataDictionary = null;
                string itemType = itemDefinitionElement.Name;
                itemDefinitionsDictionary.TryGetValue(itemType, out metadataDictionary);

                Expander expander = new Expander(properties, itemType, metadataDictionary);

                if (!Utilities.EvaluateCondition(condition, conditionAttribute, expander, ParserOptions.AllowPropertiesAndItemMetadata, parentProject))
                {
                    return;
                }

                List<XmlElement> childElements = ProjectXmlUtilities.GetValidChildElements(itemDefinitionElement);

                foreach (XmlElement child in childElements)
                {
                    EvaluateItemDefinitionChildElement(child, properties, itemDefinitionsDictionary);
                }
            }

            /// <summary>
            /// Given the properties and dictionary of previously encountered item definitions, evaluates 
            /// this specific item definition child element and adds to the dictionary as necessary.
            /// </summary>
            /// <exception cref="InvalidProjectFileException">If the item definition is incorrectly defined</exception>
            private void EvaluateItemDefinitionChildElement(XmlElement itemDefinitionChildElement, BuildPropertyGroup properties, ItemDefinitionsDictionary itemDefinitionsDictionary)
            {
                ProjectXmlUtilities.VerifyThrowProjectValidNameAndNamespace(itemDefinitionChildElement);
                ProjectErrorUtilities.VerifyThrowInvalidProject(!FileUtilities.IsItemSpecModifier(itemDefinitionChildElement.Name), itemDefinitionChildElement, "ItemSpecModifierCannotBeCustomMetadata", itemDefinitionChildElement.Name);
                ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IllegalItemPropertyNames[itemDefinitionChildElement.Name] == null, itemDefinitionChildElement, "CannotModifyReservedItemMetadata", itemDefinitionChildElement.Name);

                XmlAttribute conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(itemDefinitionChildElement, /* sole attribute */ true);
                string condition = ProjectXmlUtilities.GetAttributeValue(conditionAttribute);

                MetadataDictionary metadataDictionary = null;
                string itemType = itemDefinitionChildElement.ParentNode.Name;
                itemDefinitionsDictionary.TryGetValue(itemType, out metadataDictionary);

                Expander expander = new Expander(properties, itemType, metadataDictionary);

                if (!Utilities.EvaluateCondition(condition, conditionAttribute, expander, ParserOptions.AllowPropertiesAndItemMetadata, parentProject))
                {
                    return;
                }

                string unevaluatedMetadataValue = Utilities.GetXmlNodeInnerContents(itemDefinitionChildElement);

                bool containsItemVector = ItemExpander.ExpressionContainsItemVector(unevaluatedMetadataValue);

                // We don't allow expressions like @(foo) in the value, as no items exist at this point.
                ProjectErrorUtilities.VerifyThrowInvalidProject(!containsItemVector, itemDefinitionChildElement, "MetadataDefinitionCannotContainItemVectorExpression", unevaluatedMetadataValue, itemDefinitionChildElement.Name);

                string evaluatedMetadataValue = expander.ExpandAllIntoStringLeaveEscaped(unevaluatedMetadataValue, itemDefinitionChildElement);

                if (metadataDictionary == null)
                {
                    metadataDictionary = new MetadataDictionary(StringComparer.OrdinalIgnoreCase);
                    itemDefinitionsDictionary.Add(itemType, metadataDictionary);
                }

                // We only store the evaluated value; build items store the unevaluated value as well, but apparently only to 
                // gather recursive portions (its re-evaluation always goes back to the XML).
                // Overwrite any existing default value for this particular metadata
                metadataDictionary[itemDefinitionChildElement.Name] = evaluatedMetadataValue;
            }

            #endregion
        }
    }

    #region Related Types

    /// <summary>
    /// A limited read-only wrapper around an item definition library,
    /// specific to a particular item type.
    /// </summary>
    internal class SpecificItemDefinitionLibrary
    {
        string itemType;
        ItemDefinitionLibrary itemDefinitionLibrary;

        /// <summary>
        /// Constructor
        /// </summary>
        internal SpecificItemDefinitionLibrary(string itemType, ItemDefinitionLibrary itemDefinitionLibrary)
        {
            this.itemType = itemType;
            this.itemDefinitionLibrary = itemDefinitionLibrary;
        }

        /// <summary>
        /// Returns the item type for which this library is specific.
        /// </summary>
        internal string ItemType
        {
            get { return itemType; }
        }

        /// <summary>
        /// Get the default if any for the specified metadata name.
        /// Returns null if there is none.
        /// </summary>
        internal string GetDefaultMetadataValue(string metadataName)
        {
            return itemDefinitionLibrary.GetDefaultMetadataValue(itemType, metadataName);
        }
    }

    #endregion
}
