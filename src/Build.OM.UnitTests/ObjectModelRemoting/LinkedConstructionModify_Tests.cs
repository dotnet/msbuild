// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.UnitTests.OM.Construction;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.NetCore.Extensions;
    using Xunit.Sdk;

    public class LinkedConstructionModify_Tests : IClassFixture<LinkedConstructionModify_Tests.MyTestCollectionGroup>
    {
        public class MyTestCollectionGroup : TestCollectionGroup
        {
            public Project LocalBig { get; }
            public Project TargetBig { get; }

            internal ProjectCollectionLinker Target { get; }

            public MyTestCollectionGroup()
                : base(2, 0)
            {
                this.Target = this.Remote[0];
                this.Local.Importing = true;
            }
        }

        public MyTestCollectionGroup StdGroup { get; }
        public LinkedConstructionModify_Tests(MyTestCollectionGroup group)
        {
            this.StdGroup = group;
            group.Clear();
            this.StdGroup.Local.Importing = true;
        }

        private ProjectPair GetNewInMemoryProject(string path)
        {
            var tempPath = this.StdGroup.Disk.GetAbsolutePath(path);
            var newReal = this.StdGroup.Target.LoadInMemoryWithSettings(TestCollectionGroup.SampleProjectFile);
            newReal.Xml.FullPath = tempPath;
            var newView = this.StdGroup.Local.GetLoadedProjects(tempPath).FirstOrDefault();
            Assert.NotNull(newView);

            ViewValidation.Verify(newView, newReal);

            return new ProjectPair(newView, newReal);
        }

        [Fact]
        public void ProjectRootElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            xmlPair.VerifySetter(this.StdGroup.Disk.GetAbsolutePath("tempRenamed"), (p) => p.FullPath, (p, v) => p.FullPath = v);
            xmlPair.VerifySetter("build", (p) => p.DefaultTargets, (p, v) => p.DefaultTargets = v);
            xmlPair.VerifySetter("init", (p) => p.InitialTargets, (p, v) => p.InitialTargets = v);
            xmlPair.VerifySetter("YetAnotherSDK", (p) => p.Sdk, (p, v) => p.Sdk = v);
            xmlPair.VerifySetter("NonLocalProp", (p) => p.TreatAsLocalProperty, (p, v) => p.TreatAsLocalProperty = v);
            xmlPair.VerifySetter("xmakever", (p) => p.ToolsVersion, (p, v) => p.ToolsVersion = v);

            // Check PRE's Add"Foo" functionality.
            // grab some creation data
            var newImport = this.StdGroup.Disk.GetAbsolutePath("import");
            var newItem = this.StdGroup.Disk.GetAbsolutePath("newfile.cpp");
            var newItemWithMetadata = this.StdGroup.Disk.GetAbsolutePath("newfile2.cpp");
            List<KeyValuePair<string, string>> itemMetadata = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("m1", "v1"),
                new KeyValuePair<string, string>("m2", "v2"),
                new KeyValuePair<string, string>("m3", "v3"),
            };

            // Imports
            xmlPair.Add2NewChildrenWithVerify<ProjectImportElement>(newImport, (p, i) => p.AddImport(i), (pi, i) => pi.Project == i, out var import1, out var import2);
            xmlPair.Add2NewLabeledChildrenWithVerify<ProjectImportGroupElement>("ImportGroupLabel", (p, l) => p.AddImportGroup(), out var importGroup1, out var importGroup2);

            // Items
            xmlPair.Add2NewChildrenWithVerify<ProjectItemElement>(newItem, (p, i) => p.AddItem("cpp", i), (pi, i) => pi.Include == i, out var item1, out var item2);
            xmlPair.Add2NewChildrenWithVerify<ProjectItemElement>(newItemWithMetadata, (p, i) => p.AddItem("cpp", i, itemMetadata), (pi, i) => pi.Include == i, out var itemWithMetadata1, out var itemWithMetadata2);
            ViewValidation.VerifyMetadata(itemMetadata, (k) => itemWithMetadata1.View.Metadata.Where((md) => md.Name == k).FirstOrDefault().Value);
            xmlPair.Add2NewLabeledChildrenWithVerify<ProjectItemGroupElement>("ItemGroup", (p, l) => p.AddItemGroup(), out var itemGroup1, out var itemGroup2);

            // ItemDefs
            xmlPair.Add2NewChildrenWithVerify<ProjectItemDefinitionElement>("cpp", (p, it) => p.AddItemDefinition(it), (pi, it) => pi.ItemType == it, out var itemDefinition1, out var itemDefinition2);
            xmlPair.Add2NewLabeledChildrenWithVerify<ProjectItemDefinitionGroupElement>("ItemDefGroup", (p, l) => p.AddItemDefinitionGroup(), out var itemDefinitionGroup1, out var itemDefinitionGroup2);

            // Property
            xmlPair.Add2NewChildrenWithVerify<ProjectPropertyElement>("NewProp", (p, pn) => p.AddProperty(pn, $"Value{pn}"), (prop, pn) => prop.Name == pn, out var itemProp1, out var itemProp2);
            xmlPair.Add2NewLabeledChildrenWithVerify<ProjectPropertyGroupElement>("NewPropGroup", (p, l) => p.AddPropertyGroup(), out var itemPropretyGroup1, out var itemPropretyGroup2);

            // Target & Tasks
            xmlPair.Add2NewChildrenWithVerify<ProjectTargetElement>("NewTarget", (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n), out var newTarget1, out var newTarget2);
            xmlPair.Add2NewChildrenWithVerify<ProjectUsingTaskElement>("NewUsingTaskFile", (p, n) => p.AddUsingTask(n, "assemblyFile", null), (ut, n) => ut.TaskName == n, out var newUsinTaskFile1, out var newUsingTaskFile2);
            xmlPair.Add2NewChildrenWithVerify<ProjectUsingTaskElement>("NewUsingTaskName", (p, n) => p.AddUsingTask(n, null, "assemblyName"), (ut, n) => ut.TaskName == n, out var newUsinTaskName1, out var newUsingTaskName2);

            // loose create new element check.

            xmlPair.CreateWithVerify<ProjectChooseElement>((p) => p.CreateChooseElement());
            xmlPair.CreateWithVerify<ProjectImportElement>((p) => p.CreateImportElement("fooImport"));
            xmlPair.CreateWithVerify<ProjectImportGroupElement>((p) => p.CreateImportGroupElement());
            xmlPair.CreateWithVerify<ProjectItemDefinitionElement>((p) => p.CreateItemDefinitionElement("creteItemType"));
            xmlPair.CreateWithVerify<ProjectItemDefinitionGroupElement>((p) => p.CreateItemDefinitionGroupElement());
            xmlPair.CreateWithVerify<ProjectItemElement>((p) => p.CreateItemElement("itemType"));
            xmlPair.CreateWithVerify<ProjectItemElement>((p) => p.CreateItemElement("itemType", "include"));
            xmlPair.CreateWithVerify<ProjectItemGroupElement>((p) => p.CreateItemGroupElement());
            xmlPair.CreateWithVerify<ProjectMetadataElement>((p) => p.CreateMetadataElement("metadataName"));
            xmlPair.CreateWithVerify<ProjectMetadataElement>((p) => p.CreateMetadataElement("metadataName1", "value"));
            xmlPair.CreateWithVerify<ProjectOnErrorElement>((p) => p.CreateOnErrorElement("target"));
            xmlPair.CreateWithVerify<ProjectOtherwiseElement>((p) => p.CreateOtherwiseElement());
            xmlPair.CreateWithVerify<ProjectOutputElement>((p) => p.CreateOutputElement("taskParam", "itemType", null));
            xmlPair.CreateWithVerify<ProjectOutputElement>((p) => p.CreateOutputElement("taskParam", null, "propName"));
            xmlPair.CreateWithVerify<ProjectExtensionsElement>((p) => p.CreateProjectExtensionsElement());
            xmlPair.CreateWithVerify<ProjectSdkElement>((p) => p.CreateProjectSdkElement("sdkkk", "verrr"));
            xmlPair.CreateWithVerify<ProjectPropertyElement>((p) => p.CreatePropertyElement("name"));
            xmlPair.CreateWithVerify<ProjectPropertyGroupElement>((p) => p.CreatePropertyGroupElement());
            xmlPair.CreateWithVerify<ProjectTargetElement>((p) => p.CreateTargetElement("target"));
            xmlPair.CreateWithVerify<ProjectTaskElement>((p) => p.CreateTaskElement("task"));
            xmlPair.CreateWithVerify<ProjectUsingTaskBodyElement>((p) => p.CreateUsingTaskBodyElement("eval", "body"));
            xmlPair.CreateWithVerify<ProjectUsingTaskElement>((p) => p.CreateUsingTaskElement("taskName", "file", null));
            xmlPair.CreateWithVerify<ProjectUsingTaskElement>((p) => p.CreateUsingTaskElement("taskName", null, "name"));
            xmlPair.CreateWithVerify<ProjectUsingTaskElement>((p) => p.CreateUsingTaskElement("taskName", "file", null, "runtime", "arch"));
            xmlPair.CreateWithVerify<ProjectUsingTaskElement>((p) => p.CreateUsingTaskElement("taskName", null, "name", "runtime", "arch"));
            xmlPair.CreateWithVerify<ProjectUsingTaskParameterElement>((p) => p.CreateUsingTaskParameterElement("name", "output", "required", "paramType"));
            xmlPair.CreateWithVerify<UsingTaskParameterGroupElement>((p) => p.CreateUsingTaskParameterGroupElement());
            xmlPair.CreateWithVerify<ProjectWhenElement>((p) => p.CreateWhenElement("condition"));

            // DeepClone
            var clone = xmlPair.View.DeepClone();
            ViewValidation.IsLinkedObject(clone);
            Assert.NotSame(clone, xmlPair.View);
            Assert.True(string.IsNullOrEmpty(clone.FullPath));
        }


        [Fact]
        public void ProjectTargetElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            // create new target
            const string NewTargetName = "NewTargetName";
            var newTarget1 = xmlPair.AddNewChaildWithVerify<ProjectTargetElement>(ObjectType.View, NewTargetName, (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n));

            // add tasks to target
            const string NewTaskName = "NewTaskName";
            newTarget1.Add2NewNamedChildrenWithVerify<ProjectTaskElement>(NewTaskName, (t, n) => t.AddTask(n), out var newTask1, out var newTask2);

            // Add item groups
            const string NewTargetItemGroup = "NewTargetItemGroup";
            newTarget1.Add2NewLabeledChildrenWithVerify<ProjectItemGroupElement>(NewTargetItemGroup, (t, l) => t.AddItemGroup(), out var newItemGroup1, out var newItemGroup2);

            // Add property groups
            const string NewPropertyGroup = "NewPropertyGroup";
            newTarget1.Add2NewLabeledChildrenWithVerify<ProjectPropertyGroupElement>(NewPropertyGroup, (t, l) => t.AddPropertyGroup(), out var newPropertyGroup1, out var newPropertyGroup2);

            // Add property groups
            newTarget1.Append2NewChildrenWithVerify<ProjectOnErrorElement>("errTarget", (p, et) => p.CreateOnErrorElement(et), (oe, et)=>oe.ExecuteTargetsAttribute == et, out var newOnErr1, out var newOnErr2);


            // string setters
            newTarget1.VerifySetter("newBeforeTargets", (t) => t.BeforeTargets, (t, v) => t.BeforeTargets = v);
            newTarget1.VerifySetter("newDependsOnTargets", (t) => t.DependsOnTargets, (t, v) => t.DependsOnTargets = v);
            newTarget1.VerifySetter("newAfterTargets", (t) => t.AfterTargets, (t, v) => t.AfterTargets = v);
            newTarget1.VerifySetter("newReturns", (t) => t.Returns, (t, v) => t.Returns = v);
            newTarget1.VerifySetter("newInputs", (t) => t.Inputs, (t, v) => t.Inputs = v);
            newTarget1.VerifySetter("newOutputs", (t) => t.Outputs, (t, v) => t.Outputs = v);
            newTarget1.VerifySetter("newKeepDuplicateOutputs", (t) => t.KeepDuplicateOutputs, (t, v) => t.KeepDuplicateOutputs = v);


            newTarget1.VerifySetter("'Configuration' == 'Foo'", (t) => t.Condition, (t, v) => t.Condition = v);
            newTarget1.VerifySetter("newLabel", (t) => t.Label, (t, v) => t.Label = v);

            // rename target. First validate we do not change identity of the view
            const string NewTargetRenamed = "NewTargetRenamed";
            newTarget1.View.Name = NewTargetRenamed;
            Assert.Empty(xmlPair.QueryChildrenWithValidation<ProjectTargetElement>((t) => string.Equals(t.Name, NewTargetName)));
            newTarget1.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectTargetElement>((t) => string.Equals(t.Name, NewTargetRenamed)));

            newTarget1.Real.Name = NewTargetRenamed.Ver(2);
            Assert.Empty(xmlPair.QueryChildrenWithValidation<ProjectTargetElement>((t) => string.Equals(t.Name, NewTargetRenamed)));
            Assert.Empty(xmlPair.QueryChildrenWithValidation<ProjectTargetElement>((t) => string.Equals(t.Name, NewTargetName)));

            newTarget1.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectTargetElement>((t) => string.Equals(t.Name, NewTargetRenamed.Ver(2))));

            // this will rename back, as well as check the reqular way (after we confirmed the view identity dont change on rename).
            newTarget1.VerifySetter(NewTargetName, (t) => t.Name, (t, v) => t.Name = v);


            // removes
            newTarget1.View.RemoveChild(newTask2.View);
            Assert.ThrowsAny<ArgumentException>( () => newTarget1.Real.RemoveChild(newTask2.Real) );
            Assert.Equal(1, newTarget1.View.Tasks.Count);
            newTarget1.Real.RemoveChild(newTask1.Real);
            Assert.ThrowsAny<ArgumentException>(() => newTarget1.View.RemoveChild(newTask1.View));
            Assert.Empty(newTarget1.View.Tasks);

            Assert.NotEmpty(newTarget1.View.ItemGroups);
            Assert.NotEmpty(newTarget1.View.PropertyGroups);
            newTarget1.View.RemoveAllChildren();

            Assert.Empty(newTarget1.View.ItemGroups);
            Assert.Empty(newTarget1.View.PropertyGroups);


            newTarget1.Verify();
        }

        [Fact]
        public void ProjectTaskElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            // create new target
            const string NewTasktName = "NewTaskName";

            var newTarget = xmlPair.AddNewChaildWithVerify<ProjectTargetElement>(ObjectType.View, "TargetToTestTask", (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n));
            var newTask = newTarget.AddNewNamedChaildWithVerify<ProjectTaskElement>(ObjectType.View, NewTasktName, (t, n) => t.AddTask(n));

            Assert.Equal(0, newTask.View.Outputs.Count);
            const string NewOutputItem = "NewOutputItem";
            newTask.Add2NewChildrenWithVerify<ProjectOutputElement>(NewOutputItem, (t, n) => t.AddOutputItem(n, "CPP"), (oi, n) => oi.TaskParameter == n, out var newOutputItem1, out var newOutputItem2);
            Assert.True(newOutputItem1.View.IsOutputItem);
            Assert.False(newOutputItem1.View.IsOutputProperty);


            const string NewOutputItemWithConfig = "NewOutputItemCfg";
            newTask.Add2NewChildrenWithVerify<ProjectOutputElement>(NewOutputItemWithConfig, (t, n) => t.AddOutputItem(n, "source", "'Configuration'='Foo'"), (oi, n) => oi.TaskParameter == n, out var newOutputItemWithConfig1, out var newOutputItemWithConfig2);
            Assert.True(newOutputItemWithConfig1.View.IsOutputItem);
            Assert.False(newOutputItemWithConfig1.View.IsOutputProperty);

            const string NewOutputProperty = "NewOutputProperty";
            newTask.Add2NewChildrenWithVerify<ProjectOutputElement>(NewOutputProperty, (t, n) => t.AddOutputProperty(n, "taskprop"), (oi, n) => oi.TaskParameter == n, out var newOutputProp1, out var newOutputProp2);
            Assert.False(newOutputProp1.View.IsOutputItem);
            Assert.True(newOutputProp1.View.IsOutputProperty);


            const string NewOutputPropertyWithConfig = "NewOutputPropertyCfg";
            newTask.Add2NewChildrenWithVerify<ProjectOutputElement>(NewOutputPropertyWithConfig, (t, n) => t.AddOutputProperty(n, "source", "'Configuration'='Foo'"), (oi, n) => oi.TaskParameter == n, out var newOutputPropWithConfig1, out var newOutputPropWithConfig2);
            Assert.False(newOutputPropWithConfig1.View.IsOutputItem);
            Assert.True(newOutputPropWithConfig1.View.IsOutputProperty);

            Assert.Equal(8, newTask.View.Outputs.Count);

            newTask.VerifySetter("ErrorAndContinue", (t) => t.ContinueOnError, (t, v) => t.ContinueOnError = v);
            newTask.VerifySetter("v665+1", (t) => t.MSBuildRuntime, (t, v) => t.MSBuildRuntime = v);
            newTask.VerifySetter("msbuild256bit", (t) => t.MSBuildArchitecture, (t, v) => t.MSBuildArchitecture = v);

            // test parameters
            newTask.View.RemoveAllParameters();
            newTask.Verify();

            Assert.Equal(0, newTask.View.Parameters.Count);

            const string paramName = "paramName";
            const string paramValue = "paramValue";
            for (int i = 1; i <= 5; i++)
            {
                newTask.VerifySetter(paramValue.Ver(i), (t) => t.GetParameter(paramName.Ver(i)), (t, v) => t.SetParameter(paramName.Ver(i), v));
            }

            newTask.Verify(); 
            Assert.Equal(5, newTask.View.Parameters.Count);
            for (int i = 1; i<= 5; i++)
            {
                Assert.Equal(paramValue.Ver(i), newTask.View.Parameters[paramName.Ver(i)]);
            }

            newTask.View.RemoveParameter(paramName.Ver(1));
            newTask.Real.RemoveParameter(paramName.Ver(5));
            newTask.Verify();
            Assert.Equal(3, newTask.View.Parameters.Count);
            for (int i = 2; i <= 4; i++)
            {
                Assert.Equal(paramValue.Ver(i), newTask.View.Parameters[paramName.Ver(i)]);
            }

            Assert.False(newTask.View.Parameters.ContainsKey(paramName.Ver(1)));
            Assert.False(newTask.Real.Parameters.ContainsKey(paramName.Ver(1)));
            Assert.False(newTask.View.Parameters.ContainsKey(paramName.Ver(5)));
            Assert.False(newTask.Real.Parameters.ContainsKey(paramName.Ver(5)));


            newTask.View.RemoveAllParameters();
            newTask.Verify();
            Assert.Equal(0, newTask.View.Parameters.Count);


            newTask.View.RemoveChild(newOutputItem2.View);
            Assert.ThrowsAny<ArgumentException>(() => newTask.Real.RemoveChild(newOutputItem2.Real));
            Assert.Equal(7, newTask.View.Outputs.Count);
            newTask.Real.RemoveChild(newOutputItemWithConfig2.Real);
            Assert.ThrowsAny<ArgumentException>(() => newTask.View.RemoveChild(newOutputItem2.View));

            Assert.Equal(6, newTask.View.Outputs.Count);

            newTask.Real.RemoveChild(newOutputProp2.Real);
            Assert.Equal(5, newTask.View.Outputs.Count);
            newTask.View.RemoveChild(newOutputPropWithConfig2.View);
            Assert.Equal(4, newTask.View.Outputs.Count);

            newTask.QueryChildrenWithValidation<ProjectOutputElement>((po) => po.TaskParameter.EndsWith("1"), 4);

            newTask.Verify();
        }

        [Fact]
        public void ProjectOutputElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            var newTarget = xmlPair.AddNewChaildWithVerify<ProjectTargetElement>(ObjectType.View, "TargetToTestTask", (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n));
            var newTask = newTarget.AddNewNamedChaildWithVerify<ProjectTaskElement>(ObjectType.Real, "NewTaskName", (t, n) => t.AddTask(n));

            const string NewOutputItem = "NewOutputItem";
            const string ItemType = "CPPSource";
            var newOutputItem =  newTask.AddNewChaildWithVerify<ProjectOutputElement>(ObjectType.View, NewOutputItem, (t, n) => t.AddOutputItem(n, ItemType), (oi, n) => oi.TaskParameter == n);

            Assert.True(newOutputItem.View.IsOutputItem);
            Assert.False(newOutputItem.View.IsOutputProperty);

            const string NewOutputProperty = "NewOutputProperty";
            const string PropertyName = "OutputPropName";
            var newOutputProp = newTask.AddNewChaildWithVerify<ProjectOutputElement>(ObjectType.View, NewOutputProperty, (t, n) => t.AddOutputProperty(n, PropertyName), (oi, n) => oi.TaskParameter == n);
            Assert.False(newOutputProp.View.IsOutputItem);
            Assert.True(newOutputProp.View.IsOutputProperty);

            newOutputItem.VerifySetter(NewOutputItem.Ver(1), (o) => o.TaskParameter, (o, v) => o.TaskParameter = v);
            newOutputProp.VerifySetter(NewOutputProperty.Ver(1), (o) => o.TaskParameter, (o, v) => o.TaskParameter = v);

            newOutputItem.VerifySetter(ItemType.Ver(1), (o) => o.ItemType, (o, v) => o.ItemType = v);
            Assert.ThrowsAny<InvalidOperationException>(() => newOutputProp.View.ItemType = "foo");

            newOutputProp.VerifySetter(PropertyName.Ver(1), (o) => o.PropertyName, (o, v) => o.PropertyName = v);
            Assert.ThrowsAny<InvalidOperationException>(() => newOutputItem.View.PropertyName = "foo");
        }

        [Fact]
        public void ProjectMetadataElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            var item1 = xmlPair.AddNewChaildWithVerify<ProjectItemElement>(ObjectType.View, "newItem", (p, i) => p.AddItem("cpp", i), (pi, i) => pi.Include == i);
            var metadata = item1.AddNewChaildWithVerify<ProjectMetadataElement>(ObjectType.View, "metadata", (p, n) => p.AddMetadata(n, "value"), (md, n) => md.Name == n);

            metadata.VerifySetter("NewValue", (md) => md.Value, (md, v) => md.Value = v);
            metadata.VerifySetter("NewName", (md) => md.Name, (md, v) => md.Name = v);
            // this is tricky
            metadata.VerifySetter(true, (md) => md.ExpressedAsAttribute, (md, v) => md.ExpressedAsAttribute = v);
            xmlPair.Verify(); // this will compare all up to including the XML content of entire project
            metadata.VerifySetter(false, (md) => md.ExpressedAsAttribute, (md, v) => md.ExpressedAsAttribute = v);
            xmlPair.Verify(); // this will compare all up to including the XML content of entire project
        }

        [Fact]
        public void ProjectChooseElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            // slightly more verbose to validate some Create/Append intended semantic.
            var chooseCreataed = xmlPair.CreateWithVerify<ProjectChooseElement>((p) => p.CreateChooseElement());
            xmlPair.QueryChildrenWithValidation<ProjectChooseElement>((pc) => true, 0);

            xmlPair.View.AppendChild(chooseCreataed.View);
            var choose = xmlPair.QuerySingleChildrenWithValidation<ProjectChooseElement>((pc) => true);

            Assert.Same(choose.View, chooseCreataed.View);
            // "real" must be different, the chooseCreated real is the same remote object as the View, and chooseReal is just the second created element
            // we did for validation.
            Assert.NotSame(choose.Real, chooseCreataed.Real);

            Assert.ThrowsAny<InvalidOperationException>(() => choose.View.Condition = "ccc");

            Assert.Empty(choose.View.WhenElements);
            choose.Append2NewLabeledChildrenWithVerify<ProjectWhenElement>("when", (p, l) => p.CreateWhenElement($"'$(c)' == '{l}'"), out var when1, out var when2);
            Assert.Equal(2, choose.View.WhenElements.Count);
            when1.VerifySame(choose.QuerySingleChildrenWithValidation<ProjectWhenElement>((ch) => ch.Label == when1.View.Label));
            when2.VerifySame(choose.QuerySingleChildrenWithValidation<ProjectWhenElement>((ch) => ch.Label == when2.View.Label));

            Assert.Null(choose.View.OtherwiseElement);

            var otherWise = choose.AppendNewChaildWithVerify<ProjectOtherwiseElement>(ObjectType.View, "when", (p, l) => p.CreateOtherwiseElement(), (p,l) => true);
            Assert.Same(otherWise.View, choose.View.OtherwiseElement);
            Assert.Same(otherWise.Real, choose.Real.OtherwiseElement);

            choose.Verify();

            choose.View.RemoveChild(when2.View);
            Assert.Equal(1, choose.View.WhenElements.Count);
            when1.VerifySame(choose.QuerySingleChildrenWithValidation<ProjectWhenElement>((ch) => ch.Label == when1.View.Label));

            choose.View.RemoveChild(otherWise.View);
            Assert.Null(choose.View.OtherwiseElement);
            choose.Verify();
        }


        [Fact]
        public void ProjectWhenElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            var choose = xmlPair.AppendNewChaildWithVerify<ProjectChooseElement>(ObjectType.View, "choose", (p, s) => p.CreateChooseElement(), (p, s) => true);
            var when  = choose.AppendNewChaildWithVerify<ProjectWhenElement>(ObjectType.View, "when", (p, s) => p.CreateWhenElement("true"), (p, s) => true);

            when.VerifySetter("Condition", (we) => we.Condition, (we, v) => we.Condition = v);
            Assert.Empty(when.View.ChooseElements);
            when.Append2NewLabeledChildrenWithVerify<ProjectChooseElement>("choose", (p, l) => p.CreateChooseElement(), out var choose1, out var choose2);
            Assert.Equal(2, when.View.ChooseElements.Count);

            Assert.Empty(when.View.ItemGroups);
            when.Append2NewLabeledChildrenWithVerify<ProjectItemGroupElement>("itemGroup", (p, l) => p.CreateItemGroupElement(), out var itemGroup1, out var itemGroup2);
            Assert.Equal(2, when.View.ItemGroups.Count);

            Assert.Empty(when.View.PropertyGroups);
            when.Append2NewLabeledChildrenWithVerify<ProjectPropertyGroupElement>("propGroup", (p, l) => p.CreatePropertyGroupElement(), out var propGroup1, out var propGroup2);
            Assert.Equal(2, when.View.PropertyGroups.Count);

            when.Verify(); // will verify all collections.

            when.View.RemoveChild(choose2.View);
            Assert.Equal(1, when.View.ChooseElements.Count);
            when.Real.RemoveChild(choose1.Real);
            Assert.Empty(when.View.ChooseElements);

            when.View.RemoveChild(itemGroup2.View);
            Assert.Equal(1, when.View.ItemGroups.Count);

            when.View.RemoveChild(propGroup2.View);
            Assert.Equal(1, when.View.PropertyGroups.Count);

            when.Verify();
        }


        [Fact]
        public void ProjectOtherwiseElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);

            var choose = xmlPair.AppendNewChaildWithVerify<ProjectChooseElement>(ObjectType.View, "choose", (p, s) => p.CreateChooseElement(), (p, s) => true);
            var otherwise = choose.AppendNewChaildWithVerify<ProjectOtherwiseElement>(ObjectType.View, "when", (p, s) => p.CreateOtherwiseElement(), (p, s) => true);

            Assert.Empty(otherwise.View.ChooseElements);
            otherwise.Append2NewLabeledChildrenWithVerify<ProjectChooseElement>("choose", (p, l) => p.CreateChooseElement(), out var choose1, out var choose2);
            Assert.Equal(2, otherwise.View.ChooseElements.Count);

            Assert.Empty(otherwise.View.ItemGroups);
            otherwise.Append2NewLabeledChildrenWithVerify<ProjectItemGroupElement>("itemGroup", (p, l) => p.CreateItemGroupElement(), out var itemGroup1, out var itemGroup2);
            Assert.Equal(2, otherwise.View.ItemGroups.Count);

            Assert.Empty(otherwise.View.PropertyGroups);
            otherwise.Append2NewLabeledChildrenWithVerify<ProjectPropertyGroupElement>("propGroup", (p, l) => p.CreatePropertyGroupElement(), out var propGroup1, out var propGroup2);
            Assert.Equal(2, otherwise.View.PropertyGroups.Count);

            otherwise.Verify(); // will verify all collections.

            otherwise.View.RemoveChild(choose2.View);
            Assert.Equal(1, otherwise.View.ChooseElements.Count);
            otherwise.Real.RemoveChild(choose1.Real);
            Assert.Empty(otherwise.View.ChooseElements);

            otherwise.View.RemoveChild(itemGroup2.View);
            Assert.Equal(1, otherwise.View.ItemGroups.Count);

            otherwise.View.RemoveChild(propGroup2.View);
            Assert.Equal(1, otherwise.View.PropertyGroups.Count);

            otherwise.Verify();
        }

        [Fact]
        public void ProjectUsingTaskElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var usingTaskFile = xmlPair.AddNewChaildWithVerify<ProjectUsingTaskElement>(ObjectType.View, "NewUsingTask", (p, n) => p.AddUsingTask(n, "assemblyFile", null), (ut, n) => true);

            usingTaskFile.VerifySetter("newArgch", (ut) => ut.Architecture, (ut, v) => ut.Architecture = v);
            usingTaskFile.VerifySetter("newTaskFactory", (ut) => ut.TaskFactory, (ut, v) => ut.TaskFactory = v);
            usingTaskFile.VerifySetter("newTaskName", (ut) => ut.TaskName, (ut, v) => ut.TaskName = v);
            // this was double rename - validate overal integrity.
            usingTaskFile.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectUsingTaskElement>((ut) => true));
            usingTaskFile.VerifySetter("newAssemblyPath", (ut) => ut.AssemblyFile, (ut, v) => ut.AssemblyFile = v);
            Assert.ThrowsAny<InvalidOperationException>(() => usingTaskFile.View.AssemblyName = "xxx");
            usingTaskFile.VerifySetter("newRuntime", (ut) => ut.Runtime, (ut, v) => ut.Runtime = v);

            Assert.Null(usingTaskFile.View.TaskBody);
            var body = usingTaskFile.AddNewChaildWithVerify<ProjectUsingTaskBodyElement>(ObjectType.View, "eval", (ut, e) => ut.AddUsingTaskBody(e, "body"), (ut, e) => true);
            Assert.Same(body.View, usingTaskFile.View.TaskBody);
            Assert.Same(body.Real, usingTaskFile.Real.TaskBody);

            Assert.Null(usingTaskFile.View.ParameterGroup);
            var pg = usingTaskFile.AddNewChaildWithVerify<UsingTaskParameterGroupElement>(ObjectType.View, "pg", (ut, e) => ut.AddParameterGroup(), (ut, e) => true);
            Assert.Same(pg.View, usingTaskFile.View.ParameterGroup);
            Assert.Same(pg.Real, usingTaskFile.Real.ParameterGroup);


            xmlPair.View.RemoveChild(usingTaskFile.View);

            var usingTaskName = xmlPair.AddNewChaildWithVerify<ProjectUsingTaskElement>(ObjectType.View, "NewUsingTask", (p, n) => p.AddUsingTask(n, null, "assemblyName"), (ut, n) => true);
            usingTaskName.VerifySetter("newAssemblyName", (ut) => ut.AssemblyName, (ut, v) => ut.AssemblyName = v);
            Assert.ThrowsAny<InvalidOperationException>(() => usingTaskName.View.AssemblyFile = "xxx");
        }

        [Fact]
        public void ProjectUsingTaskBodyElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var usingTask = xmlPair.AddNewChaildWithVerify<ProjectUsingTaskElement>(ObjectType.View, "NewUsingTask", (p, n) => p.AddUsingTask(n, "assemblyFile", null), (ut, n) => true);
            // to add task body we need usingTask with factory.
            usingTask.VerifySetter("TaskFactory", (ut) => ut.TaskFactory, (ut, v) => ut.TaskFactory = v);
            var taskBody = usingTask.AddNewChaildWithVerify<ProjectUsingTaskBodyElement>(ObjectType.View, "eval", (ut, e) => ut.AddUsingTaskBody(e, "body"), (ut, e) => true);


            taskBody.VerifySetter("newBody", (tb) => tb.TaskBody, (tb, v) => tb.TaskBody = v);
            taskBody.VerifySetter("newEval", (tb) => tb.Evaluate, (tb, v) => tb.Evaluate = v);
        }

        [Fact]
        public void UsingTaskParameterGroupElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var usingTask = xmlPair.AddNewChaildWithVerify<ProjectUsingTaskElement>(ObjectType.View, "NewUsingTask", (p, n) => p.AddUsingTask(n, "assemblyFile", null), (ut, n) => true);
            // to add task param group we need usingTask with factory.
            usingTask.VerifySetter("TaskFactory", (ut) => ut.TaskFactory, (ut, v) => ut.TaskFactory = v);
            var taskParamGroup = usingTask.AddNewChaildWithVerify<UsingTaskParameterGroupElement>(ObjectType.View, "pg", (ut, e) => ut.AddParameterGroup(), (ut, e) => true);

            Assert.Empty(taskParamGroup.View.Parameters);

            taskParamGroup.Add2NewNamedChildrenWithVerify<ProjectUsingTaskParameterElement>("paraX", (tpg, n) => tpg.AddParameter(n), out var paraX1, out var paraX2);
            Assert.Equal(2, taskParamGroup.View.Parameters.Count);
            taskParamGroup.Add2NewNamedChildrenWithVerify<ProjectUsingTaskParameterElement>("paraY", (tpg, n) => tpg.AddParameter(n, "output", "required", "type"), out var paraY1, out var paraY2);
            Assert.Equal(4, taskParamGroup.View.Parameters.Count);

            taskParamGroup.Verify();
        }


        [Fact]
        public void ProjectUsingTaskParameterElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var usingTask = xmlPair.AddNewChaildWithVerify<ProjectUsingTaskElement>(ObjectType.View, "NewUsingTask", (p, n) => p.AddUsingTask(n, "assemblyFile", null), (ut, n) => true);
            usingTask.VerifySetter("TaskFactory", (ut) => ut.TaskFactory, (ut, v) => ut.TaskFactory = v);
            var taskParamGroup = usingTask.AddNewChaildWithVerify<UsingTaskParameterGroupElement>(ObjectType.View, "pg", (ut, e) => ut.AddParameterGroup(), (ut, e) => true);
            var paraElement = taskParamGroup.AddNewNamedChaildWithVerify<ProjectUsingTaskParameterElement>(ObjectType.View, "param", (tpg, n) => tpg.AddParameter(n));

            paraElement.VerifySetter("newName", (pe) => pe.Name, (pe, v) => pe.Name = v);
            paraElement.VerifySetter("newParaType", (pe) => pe.ParameterType, (pe, v) => pe.ParameterType = v);
            paraElement.VerifySetter("newOutput", (pe) => pe.Output, (pe, v) => pe.Output = v);
            paraElement.VerifySetter("newRequired", (pe) => pe.Required, (pe, v) => pe.Required = v);
        }

        [Fact]
        public void ProjectExtensionsElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var extensionXml = xmlPair.AppendNewChaildWithVerify<ProjectExtensionsElement>(ObjectType.View, "ext", (p, s) => p.CreateProjectExtensionsElement(), (pe, s) => true);

            extensionXml.VerifySetter("bla bla bla", (e) => e.Content, (e, v) => e.Content = v);
        }

        [Fact]
        public void ProjectImportElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var import = xmlPair.AddNewChaildWithVerify<ProjectImportElement>(ObjectType.View, "import", (p, s) => p.AddImport(s), (pe, s) => true);

            import.VerifySetter("newImport", (pi) => pi.Project, (pi, v) => pi.Project = v);
            import.VerifySetter("newSdk", (pi) => pi.Sdk, (pi, v) => pi.Sdk = v);
            import.VerifySetter("newVer", (pi) => pi.Version, (pi, v) => pi.Version = v);
            import.VerifySetter("newMinVer", (pi) => pi.MinimumVersion, (pi, v) => pi.MinimumVersion = v);
        }


        [Fact]
        public void ProjectImportGroupElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var importGroup = xmlPair.AddNewChaildWithVerify<ProjectImportGroupElement>(ObjectType.View, "import", (p, s) => p.AddImportGroup(), (pe, s) => true);

            Assert.Empty(importGroup.View.Imports);

            importGroup.Add2NewChildrenWithVerify<ProjectImportElement>("projFile", (ig, prj) => ig.AddImport(prj), (i, prj) => i.Project == prj, out var imp1, out var imp2);
            Assert.Equal(2, importGroup.View.Imports.Count);
        }

        [Fact]
        public void ProjectItemDefinitionElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var itemDef = xmlPair.AddNewChaildWithVerify<ProjectItemDefinitionElement>(ObjectType.View, "source", (p, s) => p.AddItemDefinition(s), (pe, s) => true);
            Assert.Equal("source", itemDef.View.ItemType);

            Assert.Empty(itemDef.View.Metadata);

            itemDef.Add2NewChildrenWithVerify<ProjectMetadataElement>("mshort", (id, n) => id.AddMetadata(n, $"value{n}"), (md, n) => md.Name == n, out var mdShort1, out var mdShort2);
            Assert.Equal(2, itemDef.View.Metadata.Count);
            itemDef.Add2NewChildrenWithVerify<ProjectMetadataElement>("mlong", (id, n) => id.AddMetadata(n, $"value{n}", false), (md, n) => md.Name == n, out var mdLong1, out var mdLong2);
            Assert.Equal(4, itemDef.View.Metadata.Count);

            itemDef.Add2NewChildrenWithVerify<ProjectMetadataElement>("mlongAttrib", (id, n) => id.AddMetadata(n, $"value{n}", true), (md, n) => md.Name == n, out var mdAttrib1, out var mdAttrib2);
            Assert.Equal(6, itemDef.View.Metadata.Count);
        }

        [Fact]
        public void ProjectItemDefinitionGroupElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var itemDefGrp = xmlPair.AddNewChaildWithVerify<ProjectItemDefinitionGroupElement>(ObjectType.View, "grp", (p, s) => p.AddItemDefinitionGroup(), (pe, s) => true);

            Assert.Empty(itemDefGrp.View.ItemDefinitions);
            itemDefGrp.Add2NewChildrenWithVerify<ProjectItemDefinitionElement>("src", (idg, it) => idg.AddItemDefinition(it), (id, n) => id.ItemType == n, out var itemDef1, out var itemDef2);
            Assert.Equal(2, itemDefGrp.View.ItemDefinitions.Count);
        }

        [Fact]
        public void ProjectItemElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var target = xmlPair.AddNewChaildWithVerify<ProjectTargetElement>(ObjectType.View, "NewTarget", (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n));
            var itemGrp = target.AddNewLabeledChaildWithVerify<ProjectItemGroupElement>(ObjectType.View, "tagetigrp", (p, s) => p.AddItemGroup());
            var itemInTargt = itemGrp.AddNewChaildWithVerify<ProjectItemElement>(ObjectType.View, "targetfile.cs", (p, s) => p.AddItem("cs", s), (pe, s) => pe.Include == s);

            var item = xmlPair.AddNewChaildWithVerify<ProjectItemElement>(ObjectType.View, "file.cpp", (p, s) => p.AddItem("cpp", s), (pe, s) => pe.Include == s);

            item.VerifySetter("newInclude", (i) => i.Include, (i, v) => i.Include = v);
            item.VerifySetter("newExclude", (i) => i.Exclude, (i, v) => i.Exclude = v);
            item.VerifySetter("newType", (i) => i.ItemType, (i, v) => i.ItemType = v);
            xmlPair.Verify(); // verify rename, thoroughly.

            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Remove = "xx"); // Include/Update/Remove are exclusive
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Update = "xx"); // Include/Update/Remove are exclusive
            item.View.Include = null;
            item.VerifySetter("newRemove", (i) => i.Remove, (i, v) => i.Remove = v);
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Include = "xx"); // Include/Update/Remove are exclusive
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Update = "xx"); // Include/Update/Remove are exclusive
            item.View.Remove = null;
            item.VerifySetter("newUpdate", (i) => i.Update, (i, v) => i.Update = v);
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Include = "xx"); // Include/Update/Remove are exclusive
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.Remove = "xx"); // Include/Update/Remove are exclusive

            // only for items inside "Target"
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.KeepMetadata = "xx");
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.KeepDuplicates = "xx");
            Assert.ThrowsAny<InvalidOperationException>(() => item.View.RemoveMetadata = "xx");

            Assert.False(item.View.HasMetadata);
            Assert.Empty(item.View.Metadata);

            item.Add2NewChildrenWithVerify<ProjectMetadataElement>("mshort", (id, n) => id.AddMetadata(n, $"value{n}"), (md, n) => md.Name == n, out var mdShort1, out var mdShort2);
            Assert.Equal(2, item.View.Metadata.Count);
            item.Add2NewChildrenWithVerify<ProjectMetadataElement>("mlong", (id, n) => id.AddMetadata(n, $"value{n}", false), (md, n) => md.Name == n, out var mdLong1, out var mdLong2);
            Assert.Equal(4, item.View.Metadata.Count);
            item.Add2NewChildrenWithVerify<ProjectMetadataElement>("mlongAttrib", (id, n) => id.AddMetadata(n, $"value{n}", true), (md, n) => md.Name == n, out var mdAttrib1, out var mdAttrib2);
            Assert.Equal(6, item.View.Metadata.Count);


            // verify target items only props.
            itemInTargt.VerifySetter("newKeepDups", (i) => i.KeepDuplicates, (i, v) => i.KeepDuplicates = v);
            itemInTargt.VerifySetter("newKeepMetadata", (i) => i.KeepMetadata, (i, v) => i.KeepMetadata = v);
            Assert.ThrowsAny<InvalidOperationException>(() => itemInTargt.View.RemoveMetadata = "xx"); // RemoveMetadata/KeepDuplicate exclusive
            itemInTargt.View.KeepMetadata = null;
            itemInTargt.VerifySetter("newRemoveMetadat", (i) => i.RemoveMetadata, (i, v) => i.RemoveMetadata = v);
            Assert.ThrowsAny<InvalidOperationException>(() => itemInTargt.View.KeepMetadata = "xx"); // RemoveMetadata/KeepDuplicate exclusive
        }

        [Fact]
        public void ProjectItemGroupElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var itemGrp = xmlPair.AddNewLabeledChaildWithVerify<ProjectItemGroupElement>(ObjectType.View, "igrp", (p, s) => p.AddItemGroup());

            Assert.Empty(itemGrp.View.Items);
            itemGrp.Add2NewChildrenWithVerify<ProjectItemElement>("file.cpp", (ig, inc) => ig.AddItem("cpp", inc), (i, inc) => i.Include == inc, out var item1, out var item2);
            Assert.Equal(2, itemGrp.View.Items.Count);

            List<KeyValuePair<string, string>> itemMetadata = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("igm1", "v1"),
                new KeyValuePair<string, string>("igm2", "v2"),
            };
            itemGrp.Add2NewChildrenWithVerify<ProjectItemElement>("file.cs", (ig, inc) => ig.AddItem("cs", inc, itemMetadata), (i, inc) => i.Include == inc, out var itemWithMetadata1, out var itemWithMetadata2);
            Assert.Equal(4, itemGrp.View.Items.Count);
            ViewValidation.VerifyMetadata(itemMetadata, (k) => itemWithMetadata1.View.Metadata.Where((md) => md.Name == k).FirstOrDefault().Value);
            ViewValidation.VerifyMetadata(itemMetadata, (k) => itemWithMetadata2.View.Metadata.Where((md) => md.Name == k).FirstOrDefault().Value);
        }

        [Fact]
        public void ProjectPropertyElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var propGrp = xmlPair.AddNewLabeledChaildWithVerify<ProjectPropertyGroupElement>(ObjectType.View, "grp", (p, l) => p.AddPropertyGroup());
            var prop = propGrp.AddNewChaildWithVerify<ProjectPropertyElement>(ObjectType.View, "prop", (pg, n) => pg.AddProperty(n, $"value{n}"), (p, n) => p.Name == n);

            prop.VerifySetter("newValue", (p) => p.Value, (p, v) => p.Value = v);
            prop.VerifySetter("newName", (p) => p.Name, (p, v) => p.Name = v);
            xmlPair.Verify(); // after rename
        }

        [Fact]
        public void ProjectPropertyGroupElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var propGrp = xmlPair.AddNewLabeledChaildWithVerify<ProjectPropertyGroupElement>(ObjectType.View, "grp", (p, l) => p.AddPropertyGroup());

            Assert.Empty(propGrp.View.Properties);
            Assert.Empty(propGrp.View.PropertiesReversed);

            propGrp.Add2NewChildrenWithVerify<ProjectPropertyElement>("prop", (pg, n) => pg.AddProperty(n, $"value{n}"), (p, n) => p.Name == n, out var prop1, out var prop2);
            Assert.Equal(2, propGrp.View.Properties.Count);
            Assert.Equal(2, propGrp.View.PropertiesReversed.Count);
            // set prop will add them if they dont exist
            propGrp.Add2NewChildrenWithVerify<ProjectPropertyElement>("setnewprop", (pg, n) => pg.SetProperty(n, $"value{n}"), (p, n) => p.Name == n, out var setNewProp1, out var setNewProp2);
            Assert.Equal(4, propGrp.View.Properties.Count);
            Assert.Equal(4, propGrp.View.PropertiesReversed.Count);
            // Add Prop will add them even if they do already exist.
            propGrp.Add2NewChildrenWithVerify<ProjectPropertyElement>("prop" /*same name*/, (pg, n) => pg.AddProperty(n, $"value2{n}"), (p, n) => p.Value == $"value2{n}", out var prop1_2, out var prop2_2);
            Assert.Equal(6, propGrp.View.Properties.Count);
            Assert.Equal(6, propGrp.View.PropertiesReversed.Count);
            prop1_2.VerifyNotSame(prop1);
            prop2_2.VerifyNotSame(prop2);
            // set prop will override them if they do.
            propGrp.Add2NewChildrenWithVerify<ProjectPropertyElement>("setnewprop" /*same name*/, (pg, n) => pg.SetProperty(n, $"value2{n}"), (p, n) => p.Value == $"value2{n}", out var setNewProp1_2, out var setNewProp2_2);
            Assert.Equal(6, propGrp.View.Properties.Count);
            Assert.Equal(6, propGrp.View.PropertiesReversed.Count);
            setNewProp1_2.VerifySame(setNewProp1);
            setNewProp2_2.VerifySame(setNewProp2);
        }

        [Fact]
        public void ProjectSdkElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var sdkElement = xmlPair.AppendNewChaildWithVerify<ProjectSdkElement>(ObjectType.View, "sdk", (p, n) => p.CreateProjectSdkElement(n, "sdkVer"), (s, n) => true);

            var curiousOfHowToSpecifySdk = xmlPair.View.RawXml;

            sdkElement.VerifySetter("newVersion", (s) => s.Version, (s, v) => s.Version = v);
            sdkElement.VerifySetter("newMinVersion", (s) => s.MinimumVersion, (s, v) => s.MinimumVersion = v);
            sdkElement.VerifySetter("newName", (s) => s.Name, (s, v) => s.Name = v);
            xmlPair.Verify();

            var curiousOfHowToSpecifySdk2 = xmlPair.View.RawXml;
        }

        [Fact]
        public void ProjectOnErrorElementModify()
        {
            var pair = GetNewInMemoryProject("temp.prj");
            var xmlPair = new ProjectXmlPair(pair);
            var newTarget = xmlPair.AddNewChaildWithVerify<ProjectTargetElement>(ObjectType.View, "TargetToTestTask", (p, n) => p.AddTarget(n), (t, n) => string.Equals(t.Name, n));
            var onErr = newTarget.AppendNewChaildWithVerify<ProjectOnErrorElement>(ObjectType.View, "errTarget", (p, et) => p.CreateOnErrorElement(et), (oe, et) => oe.ExecuteTargetsAttribute == et);

            onErr.VerifySetter("newErrTargt", (e) => e.ExecuteTargetsAttribute, (e, v) => e.ExecuteTargetsAttribute = v);
        }
    }
}

