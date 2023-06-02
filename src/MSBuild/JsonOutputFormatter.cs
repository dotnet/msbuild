// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.CommandLine
{
    internal static class JsonOutputFormatter
    {
        private JsonDictionary dictionary = new();

        internal static string ToString()
        {
            StringBuilder sb = new();
            dictionary.ToString(sb, 0);
            return sb.ToString();
        }

        internal static void AddPropertiesInJsonFormat(string[] propertyNames, Func<string, string> getProperty)
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

        internal static void AddItemInstancesInJsonFormat(string[] itemNames, ProjectInstance project)
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
                        itemDictionary.Add(metadatumName, item.GetMetadataValue(metadatumName));
                    }

                    itemArray.Add(itemDictionary);
                }

                dict.Add(itemName, itemArray);
            }

            dictionary.Add("Items", dict);
        }

        internal static void AddItemsInJsonFormat(string[] itemNames, Project project)
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
                        itemDictionary.Add(metadatumName, item.GetMetadataValue(metadatumName));
                    }

                    itemArray.Add(itemDictionary);
                }

                dict.Add(itemName, itemArray);
            }

            dictionary.Add("Items", dict);
        }

        internal static void AddTargetResultsInJsonFormat(string[] targetNames, BuildResult result)
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
                targetResultsDictionary.Add("Result", targetResult.ResultCode);
                JsonArray outputArray = new();
                foreach (ITaskItem item in targetResult.Items)
                {
                    JsonDictionary itemDict = new();
                    foreach (KeyValuePair<string, string> metadatum in item.EnumerateMetadata())
                    {
                        itemDict.Add(metadatum.Key, metadatum.Value);
                    }

                    outputArray.Add(itemDict);
                }

                targetResultsDictionary.Add("Items", outputArray);
                dict.Add(targetName, targetResultsDictionary);
            }

            dictionary.Add("Target Results", dict);
        }
    }

    private static interface JsonObject
    {
        void ToString(StringBuilder sb, int indent);
    }

    private static class JsonString : JsonObject
    {
        private string str;

        private JsonString(string s)
        {
            str = s;
        }

        public override void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine($"\"{str}\",");
        }
    }

    private static class JsonArray : JsonObject
    {
        private List<JsonDictionary> objects;
        private JsonArray()
        {
            objects = new();
        }

        public override void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.AppendLine(new string('\t', indent) + '[');
            foreach (JsonDictionary obj in objects)
            {
                obj.ToString(sb, indent + 1);
            }

            sb.AppendLine(new string('\t', indent) + ']' + ',');
        }

        private void Add(JsonDictionary obj)
        {
            objects.Add(obj);
        }
    }

    private static class JsonDictionary : JsonObject
    {
        private Dictionary<string, JsonObject> dict;
        private JsonDictionary()
        {
            dict = new();
        }

        public override void ToString(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.AppendLine(new string('\t', indent) + '{');
            foreach (KeyValuePair<string, JsonObject> kvp in dict)
            {
                sb.Append(new string('\t', indent + 1) + $"\"{kvp.Key}\": ");
                kvp.Value.ToString(sb, indent + 1);
            }

            sb.AppendLine(new string('\t', indent) + "},");
        }

        private void Add(string name, JsonObject value)
        {
            dict[name] = value;
        }
    }
}