// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Diagnostics;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// A class that evaluates an ItemGroup or PropertyGroup that is within a target.
    /// </summary>
    internal sealed class IntrinsicTask
    {
        #region Constructors

        /// <summary>
        /// Creates an IntrinsicTask object around a "task" node
        /// </summary>
        internal IntrinsicTask(XmlElement taskNodeXmlElement, EngineLoggingServices loggingServices, BuildEventContext eventContext, string executionDirectory, ItemDefinitionLibrary itemDefinitionLibrary)
        {
            this.taskNodeXmlElement = taskNodeXmlElement;
            this.conditionAttribute = taskNodeXmlElement.Attributes[XMakeAttributes.condition];
            this.loggingServices = loggingServices;
            this.buildEventContext = eventContext;
            this.executionDirectory = executionDirectory;
            this.itemDefinitionLibrary = itemDefinitionLibrary;
            
            ErrorUtilities.VerifyThrow(IsIntrinsicTaskName(taskNodeXmlElement.Name), "Only PropertyGroup and ItemGroup are known intrinsic tasks");

            switch (taskNodeXmlElement.Name)
            {
                case XMakeElements.propertyGroup:
                    backingType = BackingType.PropertyGroup;
                    // If the backing type is a property group, we can just use a property group object; its semantics aren't
                    // tangled up with the project object. Put another way, we only really need the code that understands the XML
                    // format of a property group, and we can get that without the rest of BuildPropertyGroup getting in the way.
                    // Specify that these properties are output properties, so they get reverted when the project is reset.
                    backingPropertyGroup = new BuildPropertyGroup(null /* no parent project */, taskNodeXmlElement, PropertyType.OutputProperty);
                    break;
                case XMakeElements.itemGroup:
                    backingType = BackingType.ItemGroup;
                    // If the backing type is an item group, we just re-use the code that understands the XML format of an item group;
                    // the semantics of BuildItemGroup are too coupled to its current use in the Project object for us to re-use it.
                    backingItemGroupXml = new BuildItemGroupXml(taskNodeXmlElement);
                    List<XmlElement> children = backingItemGroupXml.GetChildren();
                    backingBuildItemGroupChildren = new List<BuildItemGroupChildXml>(children.Count);

                    foreach (XmlElement child in children)
                    {
                        BuildItemGroupChildXml childXml = new BuildItemGroupChildXml(child, ChildType.Any);
                        backingBuildItemGroupChildren.Add(childXml);
                    }
                    break;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Called to execute a task within a target. This method instantiates the task, sets its parameters, and executes it. 
        /// </summary>
        internal void ExecuteTask(Lookup lookup)
        {
            ErrorUtilities.VerifyThrow(lookup != null, "Need to specify lookup.");

            if ((conditionAttribute != null) 
                && !Utilities.EvaluateCondition(conditionAttribute.Value, conditionAttribute, new Expander(lookup.ReadOnlyLookup), null, ParserOptions.AllowPropertiesAndItemLists, loggingServices, buildEventContext))
            {
                return;
            }

            // For these tasks, "execution" occurs the same whether we are asked to execute tasks
            // or merely asked to infer outputs
            switch (backingType)
            {
                case BackingType.PropertyGroup:
                    ExecutePropertyGroup(lookup);
                    break;
                case BackingType.ItemGroup:
                    ExecuteItemGroup(lookup);
                    break;
            }
        }

        /// <summary>
        /// Execute a PropertyGroup element, including each child property
        /// </summary>
        private void ExecutePropertyGroup(Lookup lookup)
        {          
            foreach (BuildProperty property in backingPropertyGroup)
            {
                ArrayList buckets = null;

                try
                {
                    // Find all the metadata references in order to create buckets
                    List<string> parameterValues = new List<string>();
                    GetBatchableValuesFromProperty(parameterValues, property);
                    buckets = BatchingEngine.PrepareBatchingBuckets(taskNodeXmlElement, parameterValues, lookup);

                    // "Execute" each bucket
                    foreach (ItemBucket bucket in buckets)
                    {
                        if (Utilities.EvaluateCondition(property.Condition, property.ConditionAttribute,
                            bucket.Expander, null, ParserOptions.AllowAll, loggingServices, buildEventContext))
                        {
                            // Check for a reserved name now, so it fails right here instead of later when the property eventually reaches
                            // the outer scope.
                            ProjectErrorUtilities.VerifyThrowInvalidProject(!ReservedPropertyNames.IsReservedProperty(property.Name), property.PropertyElement,
                                "CannotModifyReservedProperty", property.Name);

                            property.Evaluate(bucket.Expander);
                            bucket.Lookup.SetProperty(property);
                        }
                    }
                }
                finally
                {
                    if (buckets != null)
                    {
                        // Propagate the property changes to the bucket above
                        foreach (ItemBucket bucket in buckets)
                        {
                            bucket.Lookup.LeaveScope();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Execute an ItemGroup element, including each child item expression
        /// </summary>
        private void ExecuteItemGroup(Lookup lookup)
        {
            foreach (BuildItemGroupChildXml child in backingBuildItemGroupChildren)
            {
                ArrayList buckets = null;

                try
                {
                    List<string> parameterValues = new List<string>();
                    GetBatchableValuesFromBuildItemGroupChild(parameterValues, child);
                    buckets = BatchingEngine.PrepareBatchingBuckets(taskNodeXmlElement, parameterValues, lookup, child.Name);

                    // "Execute" each bucket
                    foreach (ItemBucket bucket in buckets)
                    {
                        // Gather the outputs, but don't make them visible to other buckets 
                        switch (child.ChildType)
                        {
                            case ChildType.BuildItemAdd:
                                // It's an item -- we're "adding" items to the world
                                ExecuteAdd(child, bucket);
                                break;
                            case ChildType.BuildItemRemove:
                                // It's a remove -- we're "removing" items from the world
                                ExecuteRemove(child, bucket);
                                break;
                            case ChildType.BuildItemModify:
                                // It's a modify -- changing existing items
                                ExecuteModify(child, bucket);
                                break;
                        }
                    }
                }
                finally
                {
                    if (buckets != null)
                    {
                        // Propagate the item changes to the bucket above
                        foreach (ItemBucket bucket in buckets)
                        {
                            bucket.Lookup.LeaveScope();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add items to the world. This is the in-target equivalent of an item include expression outside of a target.
        /// </summary>
        private void ExecuteAdd(BuildItemGroupChildXml child, ItemBucket bucket)
        {
            // By making the items "not persisted", we ensure they are cleaned away when the project is reset
            BuildItem item = new BuildItem(child.Element, false /* not imported */, false /* not persisted */, itemDefinitionLibrary);

            // If the condition on the item is false, Evaluate returns an empty group
            BuildItemGroup itemsToAdd = item.Evaluate(bucket.Expander, executionDirectory, true /* expand metadata */, ParserOptions.AllowAll, loggingServices, buildEventContext);

            bucket.Lookup.AddNewItems(itemsToAdd);
        }

        /// <summary>
        /// Remove items from the world. Removes to items that are part of the project manifest are backed up, so 
        /// they can be reverted when the project is reset after the end of the build.
        /// </summary>
        private void ExecuteRemove(BuildItemGroupChildXml child, ItemBucket bucket)
        {
            if (!Utilities.EvaluateCondition(child.Condition, child.ConditionAttribute, bucket.Expander, ParserOptions.AllowAll, loggingServices, buildEventContext))
            {
                return;
            }

            BuildItemGroup group = bucket.Lookup.GetItems(child.Name);
            if (group == null)
            {
                // No items of this type to remove
                return;
            }

            List<BuildItem> itemsToRemove = BuildItemGroup.FindItemsMatchingSpecification(group, child.Remove, child.RemoveAttribute, bucket.Expander, executionDirectory);

            if (itemsToRemove != null)
            {
                bucket.Lookup.RemoveItems(itemsToRemove);
            }
        }

        /// <summary>
        /// Modifies items in the world - specifically, changes their metadata. Changes to items that are part of the project manifest are backed up, so 
        /// they can be reverted when the project is reset after the end of the build.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="bucket"></param>
        private void ExecuteModify(BuildItemGroupChildXml child, ItemBucket bucket)
        {
            if (!Utilities.EvaluateCondition(child.Condition, child.ConditionAttribute, bucket.Expander, ParserOptions.AllowAll, loggingServices, buildEventContext))
            {
                return;
            }

            BuildItemGroup group = (BuildItemGroup)bucket.Lookup.GetItems(child.Name);
            if (group == null)
            {
                // No items of this type to modify
                return;
            }

            // Figure out what metadata names and values we need to set
            Dictionary<string, string> metadataToSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            List<XmlElement> metadataElements = child.GetChildren();
            foreach (XmlElement metadataElement in metadataElements)
            {
                bool metadataCondition = true;
                XmlAttribute conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(metadataElement, true /*no other attributes allowed*/);

                if (conditionAttribute != null)
                {
                    metadataCondition = Utilities.EvaluateCondition(conditionAttribute.Value, conditionAttribute, bucket.Expander, ParserOptions.AllowAll, loggingServices, buildEventContext);
                }

                if (metadataCondition)
                {
                    string unevaluatedMetadataValue = Utilities.GetXmlNodeInnerContents(metadataElement);
                    string evaluatedMetadataValue = bucket.Expander.ExpandAllIntoStringLeaveEscaped(unevaluatedMetadataValue, metadataElement);
                    // The last metadata with a particular name, wins, so we just set through the indexer here.
                    metadataToSet[metadataElement.Name] = evaluatedMetadataValue;
                }
            }

            bucket.Lookup.ModifyItems(child.Name, group, metadataToSet);
        }

        /// <summary>
        /// Adds batchable parameters from a property element into the list. If the property element was
        /// a task, these would be its raw parameter values.
        /// </summary>
        private void GetBatchableValuesFromProperty(List<string> parameterValues, BuildProperty property)
        {
            AddIfNotEmptyString(parameterValues, property.Value);
            AddIfNotEmptyString(parameterValues, property.Condition);
        }

        /// <summary>
        /// Adds batchable parameters from an item element into the list. If the item element was a task, these
        /// would be its raw parameter values.
        /// </summary>
        private void GetBatchableValuesFromBuildItemGroupChild(List<string> parameterValues, BuildItemGroupChildXml child)
        {
            AddIfNotEmptyString(parameterValues, child.Include);
            AddIfNotEmptyString(parameterValues, child.Exclude);
            AddIfNotEmptyString(parameterValues, child.Remove);
            AddIfNotEmptyString(parameterValues, child.Condition);

            List<XmlElement> metadataElements = child.GetChildren();
            foreach (XmlElement metadataElement in metadataElements)
            {
                AddIfNotEmptyString(parameterValues, Utilities.GetXmlNodeInnerContents(metadataElement));
                XmlAttribute conditionAttribute = metadataElement.Attributes[XMakeAttributes.condition];
                if (conditionAttribute != null)
                {
                    AddIfNotEmptyString(parameterValues, conditionAttribute.Value);
                }
            }
        }

        /// <summary>
        /// If value is not an empty string, adds it to list.
        /// </summary>
        private static void AddIfNotEmptyString(List<string> list, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                list.Add(value);
            }
        }

        #endregion

        #region Fields

        // the XML backing the task
        private XmlElement taskNodeXmlElement;
        // the logging services provider
        private EngineLoggingServices loggingServices;
        // event contextual information where the event is fired from
        private BuildEventContext buildEventContext;
        // whether the backing type is a property group, or an item group
        BackingType backingType;
        // backing property group, if any
        BuildPropertyGroup backingPropertyGroup;
        // backing xml for a backing item group, if any
        BuildItemGroupXml backingItemGroupXml;
        // children of the backing item group, if any
        List<BuildItemGroupChildXml> backingBuildItemGroupChildren = null;
        // directory in which the project is executing -- the current directory needed to expand wildcards
        string executionDirectory;
        // the conditional expression that controls task execution
        private XmlAttribute conditionAttribute;
        // the library of default metadata that any new items should inherit
        private ItemDefinitionLibrary itemDefinitionLibrary;
        #endregion

        #region Nested Types

        /// <summary>
        /// Used to discriminate the backing type of this object
        /// </summary>
        private enum BackingType
        {
            PropertyGroup,
            ItemGroup
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Compares the task name (case sensitively) to see
        /// if it's an "intrinsic task"
        /// </summary>
        internal static bool IsIntrinsicTaskName(string name)
        {
            return (String.Equals(name, XMakeElements.propertyGroup, StringComparison.Ordinal)
                ||  String.Equals(name, XMakeElements.itemGroup, StringComparison.Ordinal));
        }

        #endregion  
    }
}
