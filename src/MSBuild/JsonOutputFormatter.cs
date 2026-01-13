// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class is intended to take in names of properties, items, and/or target results and some means of computing
    /// those data, then format them in a json object and provide a convenient means to stringify them.
    /// </summary>
    internal sealed class JsonOutputFormatter
    {
        private static readonly JsonSerializerOptions s_options = new() { AllowTrailingCommas = false, WriteIndented = true };
        private readonly JsonNode _topLevelNode = new JsonObject();

        public override string ToString()
        {
            return _topLevelNode.ToJsonString(s_options);
        }

        internal void AddPropertiesInJsonFormat(string[] propertyNames, Func<string, string> getProperty)
        {
            if (propertyNames.Length == 0)
            {
                return;
            }

            ErrorUtilities.VerifyThrow(_topLevelNode["Properties"] is null, "Should not add multiple lists of properties to the json format.");

            JsonNode propertiesNode = new JsonObject();
            foreach (string property in propertyNames)
            {
                propertiesNode[property] = getProperty(property);
            }

            _topLevelNode["Properties"] = propertiesNode;
        }

        internal void AddItemInstancesInJsonFormat(string[] itemNames, ProjectInstance project)
        {
            if (itemNames.Length == 0)
            {
                return;
            }

            ErrorUtilities.VerifyThrow(_topLevelNode["Items"] is null, "Should not add multiple lists of items to the json format.");

            JsonNode itemsNode = new JsonObject();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItemInstance item in project.GetItems(itemName))
                {
                    JsonObject jsonItem = new();
                    jsonItem["Identity"] = item.GetMetadataValue("Identity");
                    foreach (string metadatumName in item.MetadataNames)
                    {
                        if (metadatumName.Equals("Identity"))
                        {
                            continue;
                        }

                        jsonItem[metadatumName] = TryGetMetadataValue(item, metadatumName);
                    }

                    itemArray.Add(jsonItem);
                }

                itemsNode[itemName] = itemArray;
            }

            _topLevelNode["Items"] = itemsNode;
        }

        internal void AddItemsInJsonFormat(string[] itemNames, Project project)
        {
            if (itemNames.Length == 0)
            {
                return;
            }

            ErrorUtilities.VerifyThrow(_topLevelNode["Items"] is null, "Should not add multiple lists of items to the json format.");

            JsonObject itemsNode = new();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItem item in project.GetItems(itemName))
                {
                    JsonObject jsonItem = new();
                    jsonItem["Identity"] = item.GetMetadataValue("Identity");
                    foreach (ProjectMetadata metadatum in item.Metadata)
                    {
                        jsonItem[metadatum.Name] = metadatum.EvaluatedValue;
                    }

                    foreach (string metadatumName in FileUtilities.ItemSpecModifiers.All)
                    {
                        if (metadatumName.Equals("Identity"))
                        {
                            continue;
                        }

                        jsonItem[metadatumName] = TryGetMetadataValue(item, metadatumName);
                    }

                    itemArray.Add(jsonItem);
                }

                itemsNode[itemName] = itemArray;
            }

            _topLevelNode["Items"] = itemsNode;
        }

        internal void AddTargetResultsInJsonFormat(string[] targetNames, BuildResult result)
        {
            if (targetNames.Length == 0)
            {
                return;
            }

            ErrorUtilities.VerifyThrow(_topLevelNode["TargetResults"] is null, "Should not add multiple lists of target results to the json format.");

            JsonObject targetResultsNode = new();
            foreach (string targetName in targetNames)
            {
                TargetResult targetResult = result.ResultsByTarget![targetName];
                JsonObject targetResults = new();
                targetResults["Result"] = targetResult.ResultCode.ToString();
                JsonArray outputArray = new();
                foreach (ITaskItem item in targetResult.Items)
                {
                    JsonObject jsonItem = new();
                    jsonItem["Identity"] = item.GetMetadata("Identity");
                    foreach (string metadatumName in item.MetadataNames)
                    {
                        if (metadatumName.Equals("Identity"))
                        {
                            continue;
                        }

                        jsonItem[metadatumName] = TryGetMetadata(item, metadatumName);
                    }

                    outputArray.Add(jsonItem);
                }

                targetResults["Items"] = outputArray;
                targetResultsNode[targetName] = targetResults;
            }

            _topLevelNode["TargetResults"] = targetResultsNode;
        }

        /// <summary>
        /// Attempts to get metadata from an ITaskItem. If the metadata is a built-in metadata
        /// (like FullPath, Directory, etc.) and the item spec contains illegal path characters,
        /// this will catch the InvalidOperationException and return an empty string.
        /// </summary>
        private static string TryGetMetadata(ITaskItem item, string metadataName)
        {
            try
            {
                return item.GetMetadata(metadataName);
            }
            catch (InvalidOperationException)
            {
                // Built-in metadata like FullPath, Directory, etc. require path computation.
                // If the item spec contains illegal path characters, return empty string.
                return string.Empty;
            }
        }

        /// <summary>
        /// Attempts to get metadata value from a ProjectItemInstance. If the metadata is a built-in metadata
        /// (like FullPath, Directory, etc.) and the item spec contains illegal path characters,
        /// this will catch the InvalidOperationException and return an empty string.
        /// </summary>
        private static string TryGetMetadataValue(ProjectItemInstance item, string metadataName)
        {
            try
            {
                return item.GetMetadataValue(metadataName);
            }
            catch (InvalidOperationException)
            {
                // Built-in metadata like FullPath, Directory, etc. require path computation.
                // If the item spec contains illegal path characters, return empty string.
                return string.Empty;
            }
        }

        /// <summary>
        /// Attempts to get metadata value from a ProjectItem. If the metadata is a built-in metadata
        /// (like FullPath, Directory, etc.) and the item spec contains illegal path characters,
        /// this will catch the InvalidOperationException and return an empty string.
        /// </summary>
        private static string TryGetMetadataValue(ProjectItem item, string metadataName)
        {
            try
            {
                return item.GetMetadataValue(metadataName);
            }
            catch (InvalidOperationException)
            {
                // Built-in metadata like FullPath, Directory, etc. require path computation.
                // If the item spec contains illegal path characters, return empty string.
                return string.Empty;
            }
        }
    }
}
