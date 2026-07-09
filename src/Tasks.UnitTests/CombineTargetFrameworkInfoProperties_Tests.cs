// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class CombineTargetFrameworkInfoProperties_Tests
    {
        /// <summary>
        /// https://github.com/dotnet/msbuild/issues/8320
        /// </summary>
        [Theory]
        [InlineData(null, false, "MSB3991")]
        [InlineData("", false, "MSB3991")]
        [InlineData(null, true, "MSB3992")]
        public void RootElementNameNotValid(string? rootElementName, bool UseAttributeForTargetFrameworkInfoPropertyNames, string errorCode)
        {
            MockEngine e = new MockEngine();
            var task = new CombineTargetFrameworkInfoProperties();
            task.BuildEngine = e;
            var items = new ITaskItem[]
            {
                new TaskItemData("ItemSpec1", null)
            };
            task.RootElementName = rootElementName;
            task.PropertiesAndValues = items;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = UseAttributeForTargetFrameworkInfoPropertyNames;
            task.Execute().ShouldBe(false);
            e.AssertLogContains(errorCode);
        }

        /// <summary>
        /// With the default (legacy) schema, the RootElementName becomes the root XML element
        /// and each item becomes a child element named after its ItemSpec with its Value metadata as content.
        /// </summary>
        [Fact]
        public void CombinesPropertiesIntoXmlUsingRootElementName()
        {
            var task = new CombineTargetFrameworkInfoProperties
            {
                BuildEngine = new MockEngine(),
                RootElementName = "PropertyGroup",
                PropertiesAndValues = new ITaskItem[]
                {
                    new TaskItem("Prop1", new Dictionary<string, string> { { "Value", "Val1" } }),
                    new TaskItem("Prop2", new Dictionary<string, string> { { "Value", "Val2" } }),
                },
            };

            task.Execute().ShouldBeTrue();

            XElement root = XElement.Parse(task.Result);
            root.Name.LocalName.ShouldBe("PropertyGroup");
            root.Attribute("Name").ShouldBeNull();
            root.Element("Prop1")!.Value.ShouldBe("Val1");
            root.Element("Prop2")!.Value.ShouldBe("Val2");
        }

        /// <summary>
        /// When opting into the attribute-based schema, the root element is named "TargetFramework"
        /// and the RootElementName is emitted as its "Name" attribute.
        /// </summary>
        [Fact]
        public void UsesAttributeSchemaWhenOptedIn()
        {
            var task = new CombineTargetFrameworkInfoProperties
            {
                BuildEngine = new MockEngine(),
                RootElementName = "net8.0",
                UseAttributeForTargetFrameworkInfoPropertyNames = true,
                PropertiesAndValues = new ITaskItem[]
                {
                    new TaskItem("Prop1", new Dictionary<string, string> { { "Value", "Val1" } }),
                },
            };

            task.Execute().ShouldBeTrue();

            XElement root = XElement.Parse(task.Result);
            root.Name.LocalName.ShouldBe("TargetFramework");
            root.Attribute("Name")!.Value.ShouldBe("net8.0");
            root.Element("Prop1")!.Value.ShouldBe("Val1");
        }

        /// <summary>
        /// In the attribute-based schema an empty RootElementName is allowed (only null is rejected),
        /// producing an empty "Name" attribute.
        /// </summary>
        [Fact]
        public void EmptyRootElementNameIsValidInAttributeSchema()
        {
            var task = new CombineTargetFrameworkInfoProperties
            {
                BuildEngine = new MockEngine(),
                RootElementName = "",
                UseAttributeForTargetFrameworkInfoPropertyNames = true,
                PropertiesAndValues = new ITaskItem[]
                {
                    new TaskItem("Prop1", new Dictionary<string, string> { { "Value", "Val1" } }),
                },
            };

            task.Execute().ShouldBeTrue();

            XElement root = XElement.Parse(task.Result);
            root.Name.LocalName.ShouldBe("TargetFramework");
            root.Attribute("Name")!.Value.ShouldBe("");
        }

        /// <summary>
        /// When PropertiesAndValues is null the task succeeds without producing a result.
        /// </summary>
        [Fact]
        public void NullPropertiesAndValuesSucceedsWithNoResult()
        {
            var task = new CombineTargetFrameworkInfoProperties
            {
                BuildEngine = new MockEngine(),
                RootElementName = "PropertyGroup",
                PropertiesAndValues = null,
            };

            task.Execute().ShouldBeTrue();

            task.Result.ShouldBeNull();
        }
    }
}
