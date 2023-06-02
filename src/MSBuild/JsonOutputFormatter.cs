// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.CommandLine
{
    internal class JsonOutputFormatter
    {
        private JsonDictionary dictionary = new();

        public override string ToString()
        {
            StringBuilder sb = new();
            dictionary.ToString(sb, 0);
            return sb.ToString();
        }

        internal void AddPropertiesInJsonFormat(string[] propertyNames, Func<string, string> getProperty)
        {
            if (propertyNames.Length == 0)
            {
                return;
            }

            JsonDictionary dict = new();
            foreach (string property in propertyNames)
            {
                dict.Add(property, new JsonString(getProperty(property)));
            }

            dictionary.Add("Properties", dict);
        }

        internal void AddItemInstancesInJsonFormat(string[] itemNames, ProjectInstance project)
        {
            if (itemNames.Length == 0)
            {
                return;
            }

            JsonDictionary dict = new();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItemInstance item in project.GetItems(itemName))
                {
                    JsonDictionary itemDictionary = new();
                    foreach (ProjectMetadataInstance metadatum in item.Metadata)
                    {
                        itemDictionary.Add(metadatum.Name, new JsonString(metadatum.EvaluatedValue));
                    }

                    foreach (string metadatumName in FileUtilities.ItemSpecModifiers.All)
                    {
                        itemDictionary.Add(metadatumName, new JsonString(item.GetMetadataValue(metadatumName)));
                    }

                    itemArray.Add(itemDictionary);
                }

                dict.Add(itemName, itemArray);
            }

            dictionary.Add("Items", dict);
        }

        internal void AddItemsInJsonFormat(string[] itemNames, Project project)
        {
            if (itemNames.Length == 0)
            {
                return;
            }

            JsonDictionary dict = new();
            foreach (string itemName in itemNames)
            {
                JsonArray itemArray = new();
                foreach (ProjectItem item in project.GetItems(itemName))
                {
                    JsonDictionary itemDictionary = new();
                    foreach (ProjectMetadata metadatum in item.Metadata)
                    {
                        itemDictionary.Add(metadatum.Name, new JsonString(metadatum.EvaluatedValue));
                    }

                    foreach (string metadatumName in FileUtilities.ItemSpecModifiers.All)
                    {
                        itemDictionary.Add(metadatumName, new JsonString(item.GetMetadataValue(metadatumName)));
                    }

                    itemArray.Add(itemDictionary);
                }

                dict.Add(itemName, itemArray);
            }

            dictionary.Add("Items", dict);
        }

        internal void AddTargetResultsInJsonFormat(string[] targetNames, BuildResult result)
        {
            if (targetNames.Length == 0)
            {
                return;
            }

            JsonDictionary dict = new();
            foreach (string targetName in targetNames)
            {
                TargetResult targetResult = result.ResultsByTarget[targetName];
                JsonDictionary targetResultsDictionary = new();
                targetResultsDictionary.Add("Result", new JsonString(targetResult.ResultCode.ToString()));
                JsonArray outputArray = new();
                foreach (ITaskItem item in targetResult.Items)
                {
                    JsonDictionary itemDict = new();
                    foreach (KeyValuePair<string, string> metadatum in item.EnumerateMetadata())
                    {
                        itemDict.Add(metadatum.Key, new JsonString(metadatum.Value));
                    }

                    outputArray.Add(itemDict);
                }

                targetResultsDictionary.Add("Items", outputArray);
                dict.Add(targetName, targetResultsDictionary);
            }

            dictionary.Add("Target Results", dict);
        }
    }

    internal interface IJsonObject
    {
        public void ToString(StringBuilder sb, int indent);
    }

    internal class JsonString : IJsonObject
    {
        private string str;

        internal JsonString(string s)
        {
            str = s;
        }

        public void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine($"\"{str}\",");
        }
    }

    internal class JsonArray : IJsonObject
    {
        private List<JsonDictionary> objects;
        internal JsonArray()
        {
            objects = new();
        }

        public void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.AppendLine(new string('\t', indent) + '[');
            foreach (JsonDictionary obj in objects)
            {
                obj.ToString(sb, indent + 1);
            }

            sb.AppendLine(new string('\t', indent) + ']' + ',');
        }

        internal void Add(JsonDictionary obj)
        {
            objects.Add(obj);
        }
    }

    internal class JsonDictionary : IJsonObject
    {
        private Dictionary<string, IJsonObject> dict;
        internal JsonDictionary()
        {
            dict = new();
        }

        public void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine(new string('\t', indent) + '{');
            foreach (KeyValuePair<string, IJsonObject> kvp in dict)
            {
                sb.Append(new string('\t', indent + 1) + $"\"{kvp.Key}\": ");
                if (kvp.Value is JsonDictionary)
                {
                    sb.AppendLine();
                }

                kvp.Value.ToString(sb, indent + 1);
            }

            sb.AppendLine(new string('\t', indent) + "},");
        }

        internal void Add(string name, IJsonObject value)
        {
            dict[name] = value;
        }
    }
}