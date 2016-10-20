using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Models;

namespace MigrationDefaultsConstructor
{
    public class Program
    {
        private const string c_temporaryDotnetNewMSBuildProjectName = "p";

        static void Main(string[] args)
        {
            var sdkRootPath=args[0];

            var beforeCommonSdkTargetsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.BeforeCommon.targets");
            var commonSdkTargetsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.Common.targets");
            var sdkTargetsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.targets");
            var sdkPropsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.props");
            var csharpTargetsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.CSharp.targets");
            var csharpPropsFilePath = Path.Combine(sdkRootPath, "src", "Tasks", "Microsoft.NET.Build.Tasks", "build", "Microsoft.NET.Sdk.CSharp.props");

            var beforeCommonSdkTargetsFile = ProjectRootElement.Open(beforeCommonSdkTargetsFilePath);
            var commonSdkTargetsFile = ProjectRootElement.Open(commonSdkTargetsFilePath);
            var sdkTargetsFile = ProjectRootElement.Open(sdkTargetsFilePath);
            var sdkPropsFile = ProjectRootElement.Open(sdkPropsFilePath);
            var csharpPropsFile = ProjectRootElement.Open(csharpPropsFilePath);
            var csharpTargetsFile = ProjectRootElement.Open(csharpTargetsFilePath);

            var allProperties = new List<DefaultProjectPropertyInfo>();
            var allItems = new List<DefaultProjectItemInfo>();

            AddPropertyDefault(allProperties, sdkPropsFile, "OutputType");
            AddPropertyDefault(allProperties, sdkPropsFile, "Configuration", ignoreConditions: true);
            AddPropertyDefault(allProperties, sdkPropsFile, "Platform");
            AddPropertyDefault(allProperties, sdkPropsFile, "FileAlignment");
            AddPropertyDefault(allProperties, sdkPropsFile, "PlatformTarget");
            AddPropertyDefault(allProperties, sdkPropsFile, "ErrorReport");
            AddPropertyDefault(allProperties, sdkPropsFile, "AssemblyName");
            AddPropertyDefault(allProperties, sdkPropsFile, "RootNamespace");
            AddPropertyDefault(allProperties, sdkPropsFile, "Deterministic");

            AddPropertyDefault(allProperties, csharpPropsFile, "WarningLevel");
            AddPropertyDefault(allProperties, csharpPropsFile, "NoWarn");

            AddHardcodedPropertyDefault(allProperties, "PackageRequireLicenseAcceptance", "false");

            AddConfigurationPropertyDefaults(allProperties, sdkPropsFile, "Debug");
            AddConfigurationPropertyDefaults(allProperties, sdkPropsFile, "Release");

            AddConfigurationPropertyDefaults(allProperties, csharpPropsFile, "Debug");
            AddConfigurationPropertyDefaults(allProperties, csharpPropsFile, "Release");

            AddPropertyDefault(allProperties, commonSdkTargetsFile, "VersionPrefix", ignoreConditions: true);
            AddPropertyDefault(allProperties, commonSdkTargetsFile, "AssemblyTitle", ignoreConditions: true);
            AddPropertyDefault(allProperties, commonSdkTargetsFile, "Product", ignoreConditions: true);
            AddPropertyDefault(allProperties, commonSdkTargetsFile, "NeutralLanguage", ignoreConditions: true);

            AddPropertyDefault(allProperties, beforeCommonSdkTargetsFile, "AutoUnifyAssemblyReferences", ignoreConditions: true);
            AddPropertyDefault(allProperties, beforeCommonSdkTargetsFile, "DesignTimeAutoUnify", ignoreConditions: true);
            AddPropertyDefault(allProperties, beforeCommonSdkTargetsFile, "TargetExt", ignoreConditions: true);

            AddCompileAndEmbeddedResourceDefaults(allItems, sdkTargetsFile);

            var wrapper = new SerializableMigrationDefaultsInfo()
            {
                Items = allItems,
                Properties = allProperties
            };

            var output = Path.Combine(Directory.GetCurrentDirectory(), "sdkdefaults.json");
            string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
            File.WriteAllText(output, json);
        }

        private static void AddHardcodedPropertyDefault(List<DefaultProjectPropertyInfo> allProperties, 
            string name, 
            string value, 
            string condition="", 
            string parentCondition="")
        {
            var propertyInfo = new DefaultProjectPropertyInfo
                {
                    Name = name,
                    Value = value,
                    Condition = condition,
                    ParentCondition = parentCondition
                };

            allProperties.Add(propertyInfo);
        }

        private static void AddCompileAndEmbeddedResourceDefaults(List<DefaultProjectItemInfo> allItems, ProjectRootElement msbuild)
        {
            var exclude = msbuild.Properties.Where(p=>p.Name == "DefaultExcludes").First().Value;

            var compileInclude = msbuild.Items.Where(i => i.ItemType == "Compile").First().Include;
            if (string.IsNullOrEmpty(compileInclude))
            {
                compileInclude = "**\\*.cs";
            }

            var embedInclude = msbuild.Items.Where(i => i.ItemType == "EmbeddedResource").First().Include;
            if (string.IsNullOrEmpty(embedInclude))
            {
                embedInclude = "**\\*.resx";
            }

            allItems.Add(new DefaultProjectItemInfo
            {
                ItemType = "Compile",
                Include=compileInclude,
                Exclude=exclude
            });

            allItems.Add(new DefaultProjectItemInfo
            {
                ItemType = "EmbeddedResource",
                Include=embedInclude,
                Exclude=exclude
            });
        }

        private static void AddConfigurationPropertyDefaults(List<DefaultProjectPropertyInfo> allProperties, ProjectRootElement msbuild, string config)
        {
            var configPropertyGroup = msbuild.PropertyGroups.Where(p => p.Condition.Contains("$(Configuration)") && p.Condition.Contains(config)).First();
            
            configPropertyGroup.Condition = $" '$(Configuration)' == '{config}' ";

            foreach (var property in configPropertyGroup.Properties)
            {
                var propertyInfo = new DefaultProjectPropertyInfo
                {
                    Name = property.Name,
                    Value = property.Value,
                    Condition = property.Condition,
                    ParentCondition = property.Parent.Condition
                };

                allProperties.Add(propertyInfo);
            }
        }

        private static void AddPropertyDefault(List<DefaultProjectPropertyInfo> allProperties, ProjectRootElement msbuild, string propertyName, int? index=null, bool ignoreConditions=false)
        {
            var properties = msbuild.Properties.Where(p => p.Name == propertyName).ToList();
            if (!properties.Any())
            {
                throw new Exception("property not found:" + propertyName);
            }

            if (properties.Count() > 1 && index == null)
            {
                throw new Exception("More than one property found but index is null:" + propertyName);
            }

            var property = properties[index ?? 0];

            if (ignoreConditions)
            {
                var propertyInfo = new DefaultProjectPropertyInfo
                {
                    Name = property.Name,
                    Value = property.Value,
                    Condition = null,
                    ParentCondition = null
                };

                allProperties.Add(propertyInfo);
            }
            else
            {
                var propertyInfo = new DefaultProjectPropertyInfo
                {
                    Name = property.Name,
                    Value = property.Value,
                    Condition = property.Condition,
                    ParentCondition = property.Parent.Condition
                };

                allProperties.Add(propertyInfo);
            }
        }
    }
}
