// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.CommandLine
{
    internal class JsonOutputFormatter
    {
        private static readonly JsonSerializerOptions Options = new() { AllowTrailingCommas = false, WriteIndented = true };
        private readonly JsonNode _topLevelNode = new JsonObject();

        public override string ToString()
        {
            return _topLevelNode.ToJsonString(Options);
        }

        internal void AddPropertiesInJsonFormat(string[] propertyNames, Func<string, string> getProperty)
        {
            if (propertyNames.Length == 0)
            {
                return;
            }

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

            JsonNode itemsNode = new JsonObject();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItemInstance item in project.GetItems(itemName))
                {
                    JsonObject jsonItem = new();
                    foreach (ProjectMetadataInstance metadatum in item.Metadata)
                    {
                        jsonItem[metadatum.Name] = metadatum.EvaluatedValue;
                    }

                    foreach (string metadatumName in FileUtilities.ItemSpecModifiers.All)
                    {
                        jsonItem[metadatumName] = item.GetMetadataValue(metadatumName);
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

            JsonObject itemsNode = new();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItem item in project.GetItems(itemName))
                {
                    JsonObject jsonItem = new();
                    foreach (ProjectMetadata metadatum in item.Metadata)
                    {
                        jsonItem[metadatum.Name] = metadatum.EvaluatedValue;
                    }

                    foreach (string metadatumName in FileUtilities.ItemSpecModifiers.All)
                    {
                        jsonItem[metadatumName] = item.GetMetadataValue(metadatumName);
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

            JsonObject targetResultsNode = new();
            foreach (string targetName in targetNames)
            {
                TargetResult targetResult = result.ResultsByTarget[targetName];
                JsonObject targetResults = new();
                targetResults["Result"] = targetResult.ResultCode.ToString();
                JsonArray outputArray = new();
                foreach (ITaskItem item in targetResult.Items)
                {
                    JsonObject jsonItem = new();
                    foreach (string metadatumName in item.MetadataNames)
                    {
                        jsonItem[metadatumName] = item.GetMetadata(metadatumName);
                    }

                    foreach (KeyValuePair<string, string> metadatum in item.EnumerateMetadata())
                    {
                        jsonItem[metadatum.Key] = metadatum.Value;
                    }

                    outputArray.Add(jsonItem);
                }

                targetResults["Items"] = outputArray;
                targetResultsNode[targetName] = targetResults;
            }

            _topLevelNode["TargetResults"] = targetResultsNode;
        }
    }
}