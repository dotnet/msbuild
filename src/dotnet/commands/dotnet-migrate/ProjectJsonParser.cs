using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli
{
    /// <summary>
    /// Parses select data from a project.json without relying on ProjectModel.
    /// Used to parse simple information.
    /// </summary>
    internal class ProjectJsonParser
    {
        public static string SdkPackageName => "Microsoft.DotNet.Core.Sdk";

        public string SdkPackageVersion { get; }

        public ProjectJsonParser(JObject projectJson)
        {
            SdkPackageVersion = GetSdkPackageVersion(projectJson);
        }

        private string GetSdkPackageVersion(JObject projectJson)
        {
            var sdkPackageNode = SelectJsonNodes(projectJson, property => property.Name == SdkPackageName).First();

            if (sdkPackageNode.Value.Type == JTokenType.String)
            {
                return (string)sdkPackageNode.Value;
            }
            else if (sdkPackageNode.Type == JTokenType.Object)
            {
                var sdkPackageNodeValue = (JObject)sdkPackageNode.Value;

                JToken sdkVersionNode;
                if (sdkPackageNodeValue.TryGetValue("version", out sdkVersionNode))
                {
                    return sdkVersionNode.Value<string>();
                }
                else
                {
                    throw new Exception("Unable to determine sdk version, no version node in default template.");
                }
            }
            else
            {
                throw new Exception("Unable to determine sdk version, no version information found");
            }
        }

        private IEnumerable<JProperty> SelectJsonNodes(
            JToken jsonNode,
            Func<JProperty, bool> condition,
            List<JProperty> nodeAccumulator = null)
        {
            nodeAccumulator = nodeAccumulator ?? new List<JProperty>();

            if (jsonNode.Type == JTokenType.Object)
            {
                var eligibleNodes = jsonNode.Children<JProperty>().Where(j => condition(j));
                nodeAccumulator.AddRange(eligibleNodes);

                foreach (var child in jsonNode.Children<JProperty>())
                {
                    SelectJsonNodes(child.Value, condition, nodeAccumulator: nodeAccumulator);
                }
            }
            else if (jsonNode.Type == JTokenType.Array)
            {
                foreach (var child in jsonNode.Children())
                {
                    SelectJsonNodes(child, condition, nodeAccumulator: nodeAccumulator);
                }
            }

            return nodeAccumulator;
        }
    }
}
