// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Microsoft.Build.Framework;
using BuildEngine = Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine;
using Microsoft.Win32;
using System.Text;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class Expander_Tests
    {
        [Test]
        public void ExpandAllIntoTaskItems0()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            Expander expander = new Expander(pg);

            List<TaskItem> itemsOut = expander.ExpandAllIntoTaskItems("", null);

            ObjectModelHelpers.AssertItemsMatch("", itemsOut.ToArray());
        }

        [Test]
        public void ExpandAllIntoTaskItems1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            Expander expander = new Expander(pg);

            List<TaskItem> itemsOut = expander.ExpandAllIntoTaskItems("foo", null);

            ObjectModelHelpers.AssertItemsMatch(@"foo", itemsOut.ToArray());
        }

        [Test]
        public void ExpandAllIntoTaskItems2()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            Expander expander = new Expander(pg);

            List<TaskItem> itemsOut = expander.ExpandAllIntoTaskItems("foo;bar;", null);

            ObjectModelHelpers.AssertItemsMatch(@"
                foo
                bar
                ", itemsOut.ToArray());
        }

        [Test]
        public void ExpandAllIntoTaskItems3()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            BuildItemGroup ig = new BuildItemGroup();
            ig.AddItem(new BuildItem("Compile", "foo.cs"));
            ig.AddItem(new BuildItem("Compile", "bar.cs"));

            BuildItemGroup ig2 = new BuildItemGroup();
            ig2.AddItem(new BuildItem("Resource", "bing.resx"));

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByType["Compile"] = ig;
            itemsByType["Resource"] = ig2;

            Expander expander = new Expander(pg, itemsByType);

            List<TaskItem> itemsOut = expander.ExpandAllIntoTaskItems("foo;bar;@(compile);@(resource)", null);

            ObjectModelHelpers.AssertItemsMatch(@"
                foo
                bar
                foo.cs
                bar.cs
                bing.resx
                ", itemsOut.ToArray());
        }

        [Test]
        public void ExpandAllIntoTaskItems4()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("a", "aaa");
            pg.SetProperty("b", "bbb");
            pg.SetProperty("c", "cc;dd");

            Expander expander = new Expander(pg);

            List<TaskItem> itemsOut = expander.ExpandAllIntoTaskItems("foo$(a);$(b);$(c)", null);

            ObjectModelHelpers.AssertItemsMatch(@"
                fooaaa
                bbb
                cc
                dd
                ", itemsOut.ToArray());
        }

        [Test]
        public void ExpandAllIntoBuildItems()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("a", "aaa");
            pg.SetProperty("b", "bbb");
            pg.SetProperty("c", "cc;dd");

            Expander expander = new Expander(pg);

            List<BuildItem> itemsOut = expander.ExpandAllIntoBuildItems("foo$(a);$(b);$(c);$(d", null);

            Assertion.AssertEquals(5, itemsOut.Count);
        }

        /// <summary>
        /// Regression test for bug 517356.  When there are literally zero items declared
        /// in the project, we should continue to expand item list references to empty-string
        /// rather than not expand them at all.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ZeroItemsInProjectExpandsToEmpty()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>

                    <Target Name=`Build` Condition=`'@(foo)'!=''` >
                        <Message Text=`This target should NOT run.`/>  
                    </Target>
                  
                </Project>
                ");

            logger.AssertLogDoesntContain("This target should NOT run.");

            logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <foo Include=`abc` Condition=` '@(foo)' == '' ` />
                    </ItemGroup>

                    <Target Name=`Build`>
                        <Message Text=`Item list foo contains @(foo)`/>
                    </Target>
                  
                </Project>
                ");

            logger.AssertLogContains("Item list foo contains abc");
        }

        [Test]
        public void ItemIncludeContainsMultipleItemReferences()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project DefaultTarget=`ShowProps` ToolsVersion=`3.5` xmlns=`msbuildnamespace` >
                    <PropertyGroup>
                        <OutputType>Library</OutputType>
                    </PropertyGroup>
                    <ItemGroup>
                        <CFiles Include=`foo.c;bar.c`/>
                        <ObjFiles Include=`@(CFiles->'%(filename).obj')`/>
                        <ObjFiles Include=`@(CPPFiles->'%(filename).obj')`/>
                        <CleanFiles Condition=`'$(OutputType)'=='Library'` Include=`@(ObjFiles);@(TargetLib)`/>
                    </ItemGroup>
                    <Target Name=`ShowProps`>
                        <Message Text=`Property OutputType=$(OutputType)`/>
                        <Message Text=`Item ObjFiles=@(ObjFiles)`/>
                        <Message Text=`Item CleanFiles=@(CleanFiles)`/>
                    </Target>
                </Project>
                ");

            logger.AssertLogContains("Property OutputType=Library");
            logger.AssertLogContains("Item ObjFiles=foo.obj;bar.obj");
            logger.AssertLogContains("Item CleanFiles=foo.obj;bar.obj");
        }

        /// <summary>
        /// Creates a set of complicated item metadata and properties, and items to exercise
        /// the Expander class.  The data here contains escaped characters, metadata that
        /// references properties, properties that reference items, and other complex scenarios.
        /// </summary>
        /// <param name="pg"></param>
        /// <param name="primaryItemsByName"></param>
        /// <param name="secondaryItemsByName"></param>
        /// <param name="itemMetadata"></param>
        /// <owner>RGoel</owner>
        private void CreateComplexPropertiesItemsMetadata
            (
            out ReadOnlyLookup readOnlyLookup,
            out Dictionary<string, string> itemMetadata
            )
        {
            itemMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            itemMetadata["Culture"] = "abc%253bdef;$(Gee_Aych_Ayee)";
            itemMetadata["Language"] = "english";

            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Gee_Aych_Ayee", "ghi");
            pg.SetProperty("OutputPath", @"\jk ; l\mno%253bpqr\stu");
            pg.SetProperty("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')");

            BuildItemGroup intermediateAssemblyItemGroup = new BuildItemGroup();
            BuildItem i1 = intermediateAssemblyItemGroup.AddNewItem("IntermediateAssembly", @"subdir1\engine.dll");
            i1.SetMetadata("aaa", "111");
            BuildItem i2 = intermediateAssemblyItemGroup.AddNewItem("IntermediateAssembly", @"subdir2\tasks.dll");
            i2.SetMetadata("bbb", "222");

            BuildItemGroup contentItemGroup = new BuildItemGroup();
            BuildItem i3 = contentItemGroup.AddNewItem("Content", "splash.bmp");
            i3.SetMetadata("ccc", "333");

            BuildItemGroup resourceItemGroup = new BuildItemGroup();
            BuildItem i4 = resourceItemGroup.AddNewItem("Resource", "string$(p).resx");
            i4.SetMetadata("ddd", "444");
            BuildItem i5 = resourceItemGroup.AddNewItem("Resource", "dialogs%253b.resx");
            i5.SetMetadata("eee", "555");

            BuildItemGroup contentItemGroup2 = new BuildItemGroup();
            BuildItem i6 = contentItemGroup2.AddNewItem("Content", "about.bmp");
            i6.SetMetadata("fff", "666");

            Hashtable secondaryItemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
            secondaryItemsByName["Resource"] = resourceItemGroup;
            secondaryItemsByName["Content"] = contentItemGroup2;

            Lookup lookup = LookupHelpers.CreateLookup(pg, secondaryItemsByName);

            // Add primary items
            lookup.EnterScope();
            lookup.PopulateWithItems("IntermediateAssembly", intermediateAssemblyItemGroup);
            lookup.PopulateWithItems("Content", contentItemGroup);

            readOnlyLookup = new ReadOnlyLookup(lookup);
        }

        /// <summary>
        /// Exercises ExpandAllIntoTaskItems with a complex set of data.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ExpandAllIntoTaskItemsComplex()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            List<TaskItem> taskItems = expander.ExpandAllIntoTaskItems(
                "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
                "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)", 
                 (new XmlDocument()).CreateAttribute("dummy"));

            // the following items are passed to the TaskItem constructor, and thus their ItemSpecs should be 
            // in escaped form. 
            ObjectModelHelpers.AssertItemsMatch(@"
                string$(p): ddd=444
                dialogs%253b: eee=555
                splash.bmp: ccc=333
                \jk
                l\mno%253bpqr\stu
                subdir1\: aaa=111
                subdir2\: bbb=222
                english_abc%253bdef
                ghi
                ", taskItems.ToArray());
        }

        /// <summary>
        /// Exercises ExpandAllIntoString with a complex set of data.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ExpandAllIntoStringComplex()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
            xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
                "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

            Assertion.AssertEquals(
                @"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1\;subdir2\ ; english_abc%3bdef;ghi",
                expander.ExpandAllIntoString(xmlattribute.Value, xmlattribute));

            Assertion.AssertEquals(
                @"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1\;subdir2\ ; english_abc%3bdef;ghi",
                expander.ExpandAllIntoString(xmlattribute));
        }

        /// <summary>
        /// Exercises ExpandAllIntoString with a complex set of data.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ExpandAllIntoStringLeaveEscapedComplex()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
            xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
                "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

            Assertion.AssertEquals(
                @"string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1\;subdir2\ ; english_abc%253bdef;ghi",
                expander.ExpandAllIntoStringLeaveEscaped(xmlattribute.Value, xmlattribute));

            Assertion.AssertEquals(
                @"string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1\;subdir2\ ; english_abc%253bdef;ghi",
                expander.ExpandAllIntoStringLeaveEscaped(xmlattribute));
        }

        /// <summary>
        /// Exercises ExpandAllIntoString with a string that does not need expanding. 
        /// In this case the expanded string should be reference identical to the passed in string.
        /// </summary>
        [Test]
        public void ExpandAllIntoStringExpectIdenticalReference()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");

            // Create a *non-literal* string. If we used a literal string, the CLR might (would) intern
            // it, which would mean that Expander would inevitably return a reference to the same string.
            // In real builds, the strings will never be literals, and we want to test the behavior in
            // that situation. 
            xmlattribute.Value = "abc123" + new Random().Next();
            string expandedString = expander.ExpandAllIntoStringLeaveEscaped(xmlattribute.Value, xmlattribute);

            // Verify neither string got interned, so that this test is meaningful
            Assertion.Assert(null == string.IsInterned(xmlattribute.Value));
            Assertion.Assert(null == string.IsInterned(expandedString));
            
            // Finally verify Expander indeed didn't create a new string.
            Assertion.Assert(Object.ReferenceEquals(xmlattribute.Value, expandedString));
        }

        /// <summary>
        /// Exercises ExpandAllIntoString with a complex set of data and various expander options
        /// </summary>
        [Test]
        public void ExpandAllIntoStringExpanderOptions()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            string value = @"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

            Expander expander = new Expander(lookup, itemMetadata, ExpanderOptions.ExpandProperties);

            Assertion.AssertEquals(@"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ; %(NonExistent) ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; %(Language)_%(Culture)", expander.ExpandAllIntoString(value, null));

            expander = new Expander(expander, ExpanderOptions.ExpandPropertiesAndMetadata);

            Assertion.AssertEquals(@"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ;  ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; english_abc%3bdef;ghi", expander.ExpandAllIntoString(value, null));

            expander = new Expander(expander, ExpanderOptions.ExpandAll);

            Assertion.AssertEquals(@"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1\;subdir2\ ; english_abc%3bdef;ghi", expander.ExpandAllIntoString(value, null));

            expander = new Expander(expander, ExpanderOptions.ExpandItems);

            Assertion.AssertEquals(@"string$(p);dialogs%3b ; splash.bmp ;  ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)", expander.ExpandAllIntoString(value, null));
        }

        /// <summary>
        /// Exercises ExpandAllIntoStringList with a complex set of data.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ExpandAllIntoStringListComplex()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
            xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
                "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

            List<string> expanded = expander.ExpandAllIntoStringList(xmlattribute.Value, xmlattribute);

            Assertion.AssertEquals(9, expanded.Count);
            Assertion.AssertEquals(@"string$(p)", expanded[0]);
            Assertion.AssertEquals(@"dialogs%3b", expanded[1]);
            Assertion.AssertEquals(@"splash.bmp", expanded[2]);
            Assertion.AssertEquals(@"\jk", expanded[3]);
            Assertion.AssertEquals(@"l\mno%3bpqr\stu", expanded[4]);
            Assertion.AssertEquals(@"subdir1\", expanded[5]);
            Assertion.AssertEquals(@"subdir2\", expanded[6]);
            Assertion.AssertEquals(@"english_abc%3bdef", expanded[7]);
            Assertion.AssertEquals(@"ghi", expanded[8]);

            expanded = expander.ExpandAllIntoStringList(xmlattribute);

            Assertion.AssertEquals(9, expanded.Count);
            Assertion.AssertEquals(@"string$(p)", expanded[0]);
            Assertion.AssertEquals(@"dialogs%3b", expanded[1]);
            Assertion.AssertEquals(@"splash.bmp", expanded[2]);
            Assertion.AssertEquals(@"\jk", expanded[3]);
            Assertion.AssertEquals(@"l\mno%3bpqr\stu", expanded[4]);
            Assertion.AssertEquals(@"subdir1\", expanded[5]);
            Assertion.AssertEquals(@"subdir2\", expanded[6]);
            Assertion.AssertEquals(@"english_abc%3bdef", expanded[7]);
            Assertion.AssertEquals(@"ghi", expanded[8]);
        }

        /// <summary>
        /// Exercises ExpandAllIntoStringListLeaveEscaped with a complex set of data.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ExpandAllIntoStringListLeaveEscapedComplex()
        {
            ReadOnlyLookup lookup;
            Dictionary<string, string> itemMetadata;
            CreateComplexPropertiesItemsMetadata(out lookup, out itemMetadata);

            Expander expander = new Expander(lookup, itemMetadata);

            XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
            xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
                "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

            List<string> expanded = expander.ExpandAllIntoStringListLeaveEscaped(xmlattribute.Value, xmlattribute);

            Assertion.AssertEquals(9, expanded.Count);
            Assertion.AssertEquals(@"string$(p)", expanded[0]);
            Assertion.AssertEquals(@"dialogs%253b", expanded[1]);
            Assertion.AssertEquals(@"splash.bmp", expanded[2]);
            Assertion.AssertEquals(@"\jk", expanded[3]);
            Assertion.AssertEquals(@"l\mno%253bpqr\stu", expanded[4]);
            Assertion.AssertEquals(@"subdir1\", expanded[5]);
            Assertion.AssertEquals(@"subdir2\", expanded[6]);
            Assertion.AssertEquals(@"english_abc%253bdef", expanded[7]);
            Assertion.AssertEquals(@"ghi", expanded[8]);

            expanded = expander.ExpandAllIntoStringListLeaveEscaped(xmlattribute);

            Assertion.AssertEquals(9, expanded.Count);
            Assertion.AssertEquals(@"string$(p)", expanded[0]);
            Assertion.AssertEquals(@"dialogs%253b", expanded[1]);
            Assertion.AssertEquals(@"splash.bmp", expanded[2]);
            Assertion.AssertEquals(@"\jk", expanded[3]);
            Assertion.AssertEquals(@"l\mno%253bpqr\stu", expanded[4]);
            Assertion.AssertEquals(@"subdir1\", expanded[5]);
            Assertion.AssertEquals(@"subdir2\", expanded[6]);
            Assertion.AssertEquals(@"english_abc%253bdef", expanded[7]);
            Assertion.AssertEquals(@"ghi", expanded[8]);
        }

        /// <summary>
        /// Expand property function that takes a null argument
        /// </summary>
        [Test]
        public void PropertyFunctionNullArgument()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([System.Convert]::ChangeType('null',$(SomeStuff.GetType())))", null);

            Assertion.AssertEquals("null", result);
        }

        /// <summary>
        /// Expand property function that returns a null
        /// </summary>
        [Test]
        public void PropertyFunctionNullReturn()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([System.Convert]::ChangeType(,$(SomeStuff.GetType())))", null);

            Assertion.AssertEquals("", result);
        }

        /// <summary>
        /// Expand property function that takes no arguments and returns a string
        /// </summary>
        [Test]
        public void PropertyFunctionNoArguments()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.ToUpper())", null);

            Assertion.AssertEquals("THIS IS SOME STUFF", result);
        }

        /// <summary>
        /// Expand property function that takes no arguments and returns a string (trimmed)
        /// </summary>
        [Test]
        public void PropertyFunctionNoArgumentsTrim()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("FileName", "    foobar.baz   ");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(FileName.Trim())", null);

            Assertion.AssertEquals("foobar.baz", result);
        }

        /// <summary>
        /// Expand property function that is a get property accessor
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyGet()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.Length)", null);

            Assertion.AssertEquals("18", result);
        }

        /// <summary>
        /// Expand property function which is a manual get property accessor
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyManualGet()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.get_Length())", null);

            Assertion.AssertEquals("18", result);
        }

        /// <summary>
        /// Expand property function which is a manual get property accessor and a concatenation of a constant
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyNoArgumentsConcat()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.ToLower())_goop", null);

            Assertion.AssertEquals("this is some stuff_goop", result);
        }

        /// <summary>
        /// Expand property function with a constant argument
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgument()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.SubString(13))", null);

            Assertion.AssertEquals("STUff", result);
        }

        /// <summary>
        /// Expand property function with a constant argument
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyPathRootSubtraction()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("RootPath", @"c:\this\is\the\root");
            pg.SetProperty("MyPath", @"c:\this\is\the\root\my\project\is\here.proj");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(MyPath.SubString($(RootPath.Length)))", null);

            Assertion.AssertEquals(@"\my\project\is\here.proj", result);
        }

        /// <summary>
        /// Expand property function with an argument that is a property
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgumentExpandedProperty()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.SubString(1$(Value)))", null);

            Assertion.AssertEquals("STUff", result);
        }

        /// <summary>
        /// Expand property function that has a boolean return value
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgumentBooleanReturn()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("PathRoot", @"c:\goo");
            pg.SetProperty("PathRoot2", @"c:\goop\");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(PathRoot2.Endswith(\))", null);
            Assertion.AssertEquals("True", result);
            result = expander.ExpandAllIntoStringLeaveEscaped(@"$(PathRoot.Endswith(\))", null);
            Assertion.AssertEquals("False", result);
        }

        /// <summary>
        /// Expand property function with an argument that is expanded, and a chaing of other functions.
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgumentNestedAndChainedFunction()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.SubString(1$(Value)).ToLower().SubString($(Value)))", null);

            Assertion.AssertEquals("ff", result);
        }


        /// <summary>
        /// Expand property function with chained functions on its results
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgumentChained()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.ToUpper().ToLower())", null);
            Assertion.AssertEquals("this is some stuff", result);
        }

        /// <summary>
        /// Expand property function with an argument that is a function
        /// </summary>
        [Test]
        public void PropertyFunctionPropertyWithArgumentNested()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "12345");
            pg.SetProperty("SomeStuff", "1234567890");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.SubString($(Value.get_Length())))", null);

            Assertion.AssertEquals("67890", result);
        }

        /// <summary>
        /// Expand property function that returns an generic list
        /// </summary>
        [Test]
        public void PropertyFunctionGenericListReturn()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([MSBuild]::__GetListTest())", null);

            Assertion.AssertEquals("A;B;C;D", result);
        }

        /// <summary>
        /// Expand property function that returns an array
        /// </summary>
        [Test]
        public void PropertyFunctionArrayReturn()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("List", "A-B-C-D");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(List.Split(-))", null);

            Assertion.AssertEquals("A;B;C;D", result);
        }

        /// <summary>
        /// Expand property function that returns an array
        /// </summary>
        [Test]
        public void PropertyFunctionArrayReturnManualSplitter()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("List", "A-B-C-D");
            pg.SetProperty("Splitter", "-");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(List.Split($(Splitter.ToCharArray())))", null);

            Assertion.AssertEquals("A;B;C;D", result);
        }

        private XmlAttribute dummyAttribute;
        private XmlAttribute DummyAttribute
        {
            get
            {
                if (dummyAttribute == null)
                {
                    dummyAttribute = (new XmlDocument()).CreateAttribute("foo");
                }

                return dummyAttribute;
            }
        }
        
        private bool EvaluateCondition(string conditionExpression, Expander expander)
        {
            Parser p = new Parser();
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, null, conditionExpression);
            GenericExpressionNode node = p.Parse(conditionExpression, DummyAttribute, ParserOptions.AllowAll);
            bool result = node.Evaluate(state);
            return result;
        }

        /// <summary>
        /// Expand property function that returns an array
        /// </summary>
        [Test]
        public void PropertyFunctionInCondition()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("PathRoot", @"c:\goo");
            pg.SetProperty("PathRoot2", @"c:\goop\");

            Expander expander = new Expander(pg);

            Assertion.Assert(EvaluateCondition(@"'$(PathRoot2.Endswith(`\`))' == 'true'", expander));
            Assertion.Assert(EvaluateCondition(@"'$(PathRoot.Endswith(\))' == 'false'", expander));
        }

        /// <summary>
        /// Expand property function that is invalid - properties don't take arguments
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[$(SomeStuff($(Value)))]", null);
        }


        /// <summary>
        /// Expand property function - invlaid since properties don't have properties
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid2()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[$(SomeStuff.Lgg)]", null);
        }

        /// <summary>
        /// Expand property function - invlaid since properties don't have properties and don't support '.' in them
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid3()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.ToUpper().Foo)", null);
        }

        /// <summary>
        /// Expand property function - properties don't take arguments
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid4()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Value", "3");
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[$(SomeStuff($(System.DateTime.Now)))]", null);
        }


        /// <summary>
        /// Expand property function - invalid expression
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid5()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(SomeStuff.ToLower()_goop)", null);
        }

        /// <summary>
        /// Expand property function - functions with invalid arguments
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid6()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[$(SomeStuff.Substring(HELLO!))]", null);
        }

        /// <summary>
        /// Expand property function - functions with invalid arguments
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid7()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[$(SomeStuff.Substring(-10))]", null);
        }

        /// <summary>
        /// Expand property function calls a static method with quoted arguments
        /// </summary>
        [Test]
        [ExpectedException(typeof(Microsoft.Build.BuildEngine.InvalidProjectFileException))]
        public void PropertyFunctionInvalid8()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(([System.DateTime]::Now).ToString(\"MM.dd.yyyy\"))", null);
        }

        /// <summary>
        /// Expand property function - we don't handle metadata functions
        /// </summary>
        [Test]
        public void PropertyFunctionInvalidNoMetadataFunctions()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("[%(LowerLetterList.Identity.ToUpper())]", null);

            Assertion.AssertEquals("[%(LowerLetterList.Identity.ToUpper())]", result);
        }

        /// <summary>
        /// Expand property function - properties won't get confused with a type or namespace
        /// </summary>
        [Test]
        public void PropertyFunctionNoCollisionsOnType()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("System", "The System Namespace");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$(System)", null);

            Assertion.AssertEquals("The System Namespace", result);
        }



        /// <summary>
        /// Expand property function that creates an instance of a type
        /// </summary>
        [Test]
        public void PropertyFunctionConstructor1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("ver1", @"1.2.3.4");


            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Version]::new($(ver1)))", null);

            Version v = new Version(result);

            Assertion.AssertEquals(1, v.Major);
            Assertion.AssertEquals(2, v.Minor);
            Assertion.AssertEquals(3, v.Build);
            Assertion.AssertEquals(4, v.Revision);
        }

        /// <summary>
        /// Expand property function that creates an instance of a type
        /// </summary>
        [Test]
        public void PropertyFunctionConstructor2()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("ver1", @"1.2.3.4");
            pg.SetProperty("ver2", @"2.2.3.4");

            Expander expander = new Expander(pg);
            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Version]::new($(ver1)).CompareTo($([System.Version]::new($(ver2)))))", null);

            Assertion.AssertEquals(@"-1", result);
        }

        /// <summary>
        /// Expand property function calls a static method 
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodMakeRelative()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("ParentPath", @"c:\abc\def");
            pg.SetProperty("FilePath", @"c:\abc\def\foo.cpp");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::MakeRelative($(ParentPath), `$(FilePath)`))", null);

            Assertion.AssertEquals(@"foo.cpp", result);
        }

        [Test]
        public void PropertyFunctionGetRegistryValueFromView1()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty("SomeProperty", "Value");

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue(String.Empty, "%TEMP%", RegistryValueKind.ExpandString);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, RegistryView.Default, RegistryView.Default))", null);

                Assertion.AssertEquals(Environment.GetEnvironmentVariable("TEMP"), result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void PropertyFunctionGetRegistryValueFromView2()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty("SomeProperty", "Value");

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue(String.Empty, "%TEMP%", RegistryValueKind.ExpandString);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, Microsoft.Win32.RegistryView.Default))", null);

                Assertion.AssertEquals(Environment.GetEnvironmentVariable("TEMP"), result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void PropertyFunctionGetRegistryValueFromView_NonexistentKey()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeProperty", "Value");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"a$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuildKeyThatDoesNotExist', null, null, Microsoft.Win32.RegistryView.Default))b", null);

            Assertion.AssertEquals("ab", result);
        }

        /// <summary>
        /// Expand property function calls a static method 
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethod1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("Drive", @"c:\");
            pg.SetProperty("File", @"foobar\baz.txt");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.IO.Path]::Combine($(Drive), `$(File)`))", null);

            Assertion.AssertEquals(@"c:\foobar\baz.txt", result);
        }

        /// <summary>
        /// Expand property function calls a static method 
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodQuoted1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("File", @"foobar\baz.txt");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.IO.Path]::Combine(`c:\`, `$(File)`))", null);

            Assertion.AssertEquals(@"c:\foobar\baz.txt", result);
        }

        /// <summary>
        /// Expand property function calls a static method with quoted arguments
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodQuoted2()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([System.DateTime]::Parse('2005/12/25').ToString(\"yyyy/MM/dd HH:mm:ss\"))", null);

            Assertion.AssertEquals(@"2005/12/25 00:00:00", result);
        }

        /// <summary>
        /// Expand property function calls a static method with quoted arguments
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodQuoted3()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([System.DateTime]::Parse('2005/12/25').ToString(\"MM.dd.yyyy\"))", null);

            Assertion.AssertEquals(@"12.25.2005", result);
        }

        /// <summary>
        /// Expand property function calls a static method with quoted arguments
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodQuoted4()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped("$([System.DateTime]::Now.ToString(\"MM.dd.yyyy\"))", null);

            Assertion.AssertEquals(DateTime.Now.ToString("MM.dd.yyyy"), result);
        }
        
        /// <summary>
        /// Expand property function calls a static method 
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodNested()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("File", @"foobar\baz.txt");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.IO.Path]::Combine(`c:\`, $([System.IO.Path]::Combine(`foobar`,`baz.txt`))))", null);

            Assertion.AssertEquals(@"c:\foobar\baz.txt", result);
        }

        /// <summary>
        /// Expand property function calls a static method regex
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodRegex1()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            // Support enum combines as Enum.Parse expects them
            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, `RegexOptions.IgnoreCase,RegexOptions.Singleline`))", null);

            Assertion.AssertEquals(@"True", result);

            // We support the C# style enum combining syntax too
            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, System.Text.RegularExpressions.RegexOptions.IgnoreCase|RegexOptions.Singleline))", null);

            Assertion.AssertEquals(@"True", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Text.RegularExpressions.Regex]::IsMatch(`100 GBP`, `^-?\d+(\.\d{2})?$`))", null);

            Assertion.AssertEquals(@"False", result);
        }

        /// <summary>
        /// Expand property function calls a static method  with an instance method chained
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodChained()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.DateTime]::Parse(`2005/12/25`).ToString(`yyyy/MM/dd HH:mm:ss`))", null);

            Assertion.AssertEquals(@"2005/12/25 00:00:00", result);
        }

        /// <summary>
        /// Expand property function calls a static method an enum argument
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodEnumArgument()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.Environment]::GetFolderPath(SpecialFolder.System))", null);

            Assertion.AssertEquals(System.Environment.GetFolderPath(Environment.SpecialFolder.System), result);
        }

        /// <summary>
        /// Expand intrinsic property function to locate the directory of a file above
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodDirectoryNameOfFileAbove()
        {
            string tempPath = Path.GetTempPath();
            string tempFile = Path.GetFileName(Path.GetTempFileName());

            try
            {
                string directoryStart = Path.Combine(tempPath, "one\\two\\three\\four\\five");

                BuildPropertyGroup pg = new BuildPropertyGroup();
                pg.SetProperty("StartingDirectory", directoryStart);
                pg.SetProperty("FileToFind", tempFile);

                Expander expander = new Expander(pg);

                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), $(FileToFind)))", null);

                Assertion.AssertEquals(Microsoft.Build.BuildEngine.Shared.FileUtilities.EnsureTrailingSlash(tempPath), Microsoft.Build.BuildEngine.Shared.FileUtilities.EnsureTrailingSlash(result));

                result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), Hobbits))", null);

                Assertion.AssertEquals(String.Empty, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        /// Expand property function calls a static arithmetic method
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodArithmeticAddInt32()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Add(40, 2))", null);

            Assertion.AssertEquals("42", result);
        }

        /// <summary>
        /// Expand property function calls a static arithmetic method
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodArithmeticAddDouble()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Add(39.9, 2.1))", null);

            Assertion.AssertEquals("42", result);
        }

        /// <summary>
        /// Expand property function calls a static bitwise method to retrieve file attribute
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodFileAttributes()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string tempFile = Path.GetTempFileName();
            try
            {

                File.SetAttributes(tempFile, FileAttributes.ReadOnly | FileAttributes.Archive);

                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes(" + tempFile + "))))", null);

                Assertion.AssertEquals("32", result);
            }
            finally
            {
                File.SetAttributes(tempFile, FileAttributes.Normal);
                File.Delete(tempFile);
            }
        }

        /// <summary>
        /// Expand intrinsic property function calls a static arithmetic method
        /// </summary>
        [Test]
        public void PropertyFunctionStaticMethodIntrinsicMaths()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Add(39.9, 2.1))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Add(40, 2))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Subtract(44, 2))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Subtract(42.9, 0.9))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Multiply(21, 2))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Multiply(84.0, 0.5))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Divide(84, 2))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Divide(84.4, 2.0))", null);

            Assertion.AssertEquals("42.2", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Modulo(85, 2))", null);

            Assertion.AssertEquals("1", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Modulo(2345.5, 43))", null);

            Assertion.AssertEquals("23.5", result);

            // test for overflow wrapping
            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::Add(9223372036854775807, 20))", null);

            Assertion.AssertEquals("9.22337203685478E+18", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::BitwiseOr(40, 2))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::BitwiseAnd(42, 2))", null);

            Assertion.AssertEquals("2", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::BitwiseXor(213, 255))", null);

            Assertion.AssertEquals("42", result);

            result = expander.ExpandAllIntoStringLeaveEscaped(@"$([MSBuild]::BitwiseNot(-43))", null);

            Assertion.AssertEquals("42", result);

        }
        /// <summary>
        /// Expand a property reference that has whitespace around the property name (should result in empty)
        /// </summary>
        [Test]
        public void PropertySimpleSpaced()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomeStuff", "This IS SOME STUff");

            Expander expander = new Expander(pg);

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$( SomeStuff )", null);

            Assertion.AssertEquals(String.Empty, result);
        }

        /// <summary>
        /// Expand a property function that references item metadata
        /// </summary>
        [Test]
        public void PropertyFunctionConsumingItemMetadata()
        {
            MockLogger logger = new MockLogger();
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                </Project>
            ", logger);


            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("SomePath", @"c:\some\path");

            BuildItemGroup ig = new BuildItemGroup();
            BuildItem item = new BuildItem("Compile", "fOo.Cs");
            item.SetMetadata("SomeMeta", "fOo.Cs");
            ig.AddItem(item);

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByType["Compile"] = ig;

            Expander expander = new Expander(pg, itemsByType, ExpanderOptions.ExpandAll);
            expander.SetMetadataInMetadataTable("Compile", "SomeMeta", "fOo.Cs");

            string result = expander.ExpandAllIntoStringLeaveEscaped(@"$([System.IO.Path]::Combine($(SomePath),%(Compile.SomeMeta)))", null);

            Assertion.AssertEquals(@"c:\some\path\fOo.Cs", result);
        }

        /// <summary>
        /// A whole bunch error check tests
        /// </summary>
        [Test]
        public void Medley()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("File", @"foobar\baz.txt");

            pg.SetProperty("a", "no");
            pg.SetProperty("b", "true");
            pg.SetProperty("c", "1");
            pg.SetProperty("d", "xxx");
            pg.SetProperty("e", "xxx");
            pg.SetProperty("and", "and");
            pg.SetProperty("a_semi_b", "a;b");
            pg.SetProperty("a_apos_b", "a'b");
            pg.SetProperty("foo_apos_foo", "foo'foo");
            pg.SetProperty("a_escapedsemi_b", "a%3bb");
            pg.SetProperty("a_escapedapos_b", "a%27b");
            pg.SetProperty("has_trailing_slash", @"foo\");
            pg.SetProperty("emptystring", @"");
            pg.SetProperty("space", @" ");
            pg.SetProperty("propertycontainingnullasastring", @"null");

            Expander expander = new Expander(pg);

            string[,] validTests = {
                {"$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', 16))))", "42"},
                {"$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', $([System.Convert]::ToInt32(16))))))", "42"},
                {"$(e.Length.ToString())", "3"},
                {"$(e.get_Length().ToString())", "3"},
                {"$(emptystring.Length)", "0" },
                {"$(space.Length)", "1" },
                {"$([System.TimeSpan]::Equals(null, null))", "True"}, // constant, unquoted null is a special value
                {"$([MSBuild]::Add(40,null))", "40"},
                {"$([MSBuild]::Add( 40 , null ))", "40"},
                {"$([MSBuild]::Add(null,40))", "40"},
                {"$([MSBuild]::Escape(';'))", "%3b"},
                {"$([MSBuild]::UnEscape('%3b'))", ";"},
                {"$(e.Substring($(e.Length)))", ""},
                {"$(386)", ""},
                {"$(Environent:W3=w2)", ""},
                {"$([System.Int32]::MaxValue)", System.Int32.MaxValue.ToString()},
                                   };

            string[] errorTests = {
                "$([Microsoft.VisualBasic.FileIO.FileSystem]::CurrentDirectory)", // not allowed
                "$(e.Length..ToString())",
                "$(SomeStuff.get_Length(null))",
                "$(SomeStuff.Substring((1)))",
                "$(b.Substring(-10, $(c)))",
                "$(b.Substring(-10, $(emptystring)))",
                "$(b.Substring(-10, $(space)))",
                "$([MSBuild]::Add.Sub(null,40))",
                "$([MSBuild]::Add( ,40))", // empty parameter is empty string
                "$([MSBuild]::Add('',40))", // empty quoted parameter is empty string
                "$([MSBuild]::Add(40,,,))",
                "$([MSBuild]::Add(40, ,,))",
                "$([MSBuild]::Add(40,)",
                "$([MSBuild]::Add(40,X)",
                "$([MSBuild]::Add(40,",
                "$([MSBuild]::Add(40",
                "$([MSBuild]::Add(,))", // gives "Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true."
                "$([System.TimeSpan]::Equals(,))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(space),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(emptystring),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([MSBuild]::Add($(PropertyContainingNullAsAString),40))", // a property containing the word null is a string "null"
                "$([MSBuild]::Add('null',40))", // the word null is a string "null"
                "$(SomeStuff.Substring(-10))",
                "$(.Length)",
                "$(.Substring(1))",
                "$(.get_Length())",
                "$(e.)",
                "$(e..)",
                "$(e..Length)",
                "$(e$(d).Length)",
                "$($(d).Length)",
                "$(e`.Length)",
                "$([System.IO.Path]Combine::Combine(`a`,`b`))",
                "$([System.IO.Path]::Combine((`a`,`b`))",
                "$([System.IO.Path]::Combine(`|`,`b`))",
                "$([System.IO.Path]Combine(::Combine(`a`,`b`))",
                "$([System.IO.Path]Combine(`::Combine(`a`,`b`)`, `b`)`)",
                "$([System.IO.Path]::`Combine(`a`, `b`)`)",
                "$([System.IO.Path]::(`Combine(`a`, `b`)`))",
                "$([System.DateTime]foofoo::Now)",
                "$([System.DateTime].Now)",
                "$([].Now)",
                "$([ ].Now)",
                "$([ .Now)",
                "$([])",
                "$([ )",
                "$([ ])",
                "$([System.Diagnostics.Process]::Start(`NOTEPAD.EXE`))",
                "$([[]]::Start(`NOTEPAD.EXE`))",
                "$([(::Start(`NOTEPAD.EXE`))",
                "$([Goop]::Start(`NOTEPAD.EXE`))",
                "$([System.Threading.Thread]::CurrentThread)",
                "$",
                "$(",
                "$((",
                "@",
                "@(",
                "@()",
                "%",
                "%(",
                "%()",
                "exists",
                "exists(",
                "exists()",
                "exists( )",
                "exists(,)",
                "@(x->'",
                "@(x->''",
                "@(x-",
                "@(x->'x','",
                "@(x->'x',''",
                "@(x->'x','')",
                "-1>x",
                "\n",
                "\t",
                "+-1",
                "$(SomeStuff.)",
                "$(SomeStuff.!)",
                "$(SomeStuff.`)",
                "$(SomeStuff.GetType)",
                "$(goop.baz`)",
                "$(SomeStuff.Substring(HELLO!))",
                "$(SomeStuff.ToLower()_goop)",
                "$(SomeStuff($(System.DateTime.Now)))",
                "$(System.Foo.Bar.Lgg)",
                "$(SomeStuff.Lgg)",
                "$(SomeStuff($(Value)))",
                "$(e.$(e.Length))",
                "$(e.Substring($(e.Substring(,)))",
                "$(e.Substring($(e.Substring(a)))",
                "$(e.Substring($([System.IO.Path]::Combine(`a`, `b`))))",

            };

            string result;
            for (int i = 0; i < validTests.GetLength(0); i++)
            {
                result = expander.ExpandAllIntoStringLeaveEscaped(validTests[i, 0], null);

                if (!String.Equals(result, validTests[i, 1]))
                {
                    Console.WriteLine("FAILURE: " + validTests[i, 0] + " expanded to '" + result + "' instead of '" + validTests[i, 1] + "'");
                }
                else
                {
                    Console.WriteLine(validTests[i, 0] + " expanded to '" + result + "'");
                }
            }

            for (int i = 0; i < errorTests.GetLength(0); i++)
            {
                // If an expression is invalid,
                //      - Expansion may throw InvalidProjectFileException, or
                //      - return the original unexpanded expression
                bool success = true;
                bool caughtException = false;
                result = String.Empty;
                try
                {
                    result = expander.ExpandAllIntoStringLeaveEscaped(errorTests[i], null);
                    if (String.Compare(result, errorTests[i]) == 0)
                    {
                        Console.WriteLine(errorTests[i] + " did not expand.");
                        success = false;
                    }
                }
                catch (Microsoft.Build.BuildEngine.InvalidProjectFileException ex)
                {
                    Console.WriteLine(errorTests[i] + " caused '" + ex.Message + "'");
                    caughtException = true;
                }
                Assertion.Assert("FAILURE: Expected '" + errorTests[i] + "' to not parse or not be evaluated but it evaluated to '" + result + "'",
                    (success == false || caughtException == true));

            }
        }

        [Test]
        public void RegistryPropertyString()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue("Value", "String", RegistryValueKind.String);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals("String", result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void RegistryPropertyBinary()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                UTF8Encoding enc = new UTF8Encoding();
                byte[] utfText = enc.GetBytes("String".ToCharArray());

                key.SetValue("Value", utfText, RegistryValueKind.Binary);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals("83;116;114;105;110;103", result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void RegistryPropertyDWord()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue("Value", 123456, RegistryValueKind.DWord);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals("123456", result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void RegistryPropertyExpandString()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue("Value", "%TEMP%", RegistryValueKind.ExpandString);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals(Environment.GetEnvironmentVariable("TEMP"), result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void RegistryPropertyQWord()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue("Value", (long)123456789123456789, RegistryValueKind.QWord);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals("123456789123456789", result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }

        [Test]
        public void RegistryPropertyMultiString()
        {
            try
            {
                BuildPropertyGroup pg = new BuildPropertyGroup();

                Expander expander = new Expander(pg);
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

                key.SetValue("Value", new string[] { "A", "B", "C", "D" }, RegistryValueKind.MultiString);
                string result = expander.ExpandAllIntoStringLeaveEscaped(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)", null);

                Assertion.AssertEquals("A;B;C;D", result);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
            }
        }    
    }
 
    /// <summary>
    /// Tests relating to SplitSemiColonSeparatedList method
    /// </summary>
    [TestFixture]
    public class SplitSemiColonSeparatedList_Tests
    {
        [Test]
        public void NoOpSplit()
        {
            VerifySplitSemiColonSeparatedList("a", "a");
        }

        [Test]
        public void BasicSplit()
        {
            VerifySplitSemiColonSeparatedList("a;b", "a", "b");
        }

        [Test]
        public void Empty()
        {
            VerifySplitSemiColonSeparatedList("", null);
        }

        [Test]
        public void SemicolonOnly()
        {
            VerifySplitSemiColonSeparatedList(";", null);
        }

        [Test]
        public void TwoSemicolons()
        {
            VerifySplitSemiColonSeparatedList(";;", null);
        }

        [Test]
        public void TwoSemicolonsAndOneEntryAtStart()
        {
            VerifySplitSemiColonSeparatedList("a;;", "a");
        }

        [Test]
        public void TwoSemicolonsAndOneEntryAtEnd()
        {
            VerifySplitSemiColonSeparatedList(";;a", "a");
        }

        [Test]
        public void AtSignAtEnd()
        {
            VerifySplitSemiColonSeparatedList("@", "@");
        }

        [Test]
        public void AtSignParenAtEnd()
        {
            VerifySplitSemiColonSeparatedList("foo@(", "foo@(");
        }

        [Test]
        public void EmptyEntriesRemoved()
        {
            VerifySplitSemiColonSeparatedList(";a;bbb;;c;;", "a", "bbb", "c");
        }

        [Test]
        public void EntriesTrimmed()
        {
            VerifySplitSemiColonSeparatedList("  ;  a   ;b   ;   ;c\n;  \r;  ", "a", "b", "c");
        }

        [Test]
        public void NoSplittingOnMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';')", "@(foo->';')");
        }

        [Test]
        public void NoSplittingOnSeparators()
        {
            VerifySplitSemiColonSeparatedList("@(foo, ';')", "@(foo, ';')");
        }

        [Test]
        public void NoSplittingOnSeparatorsAndMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->'abc;def', 'ghi;jkl')", "@(foo->'abc;def', 'ghi;jkl')");
        }

        [Test]
        public void CloseParensInMacro()
        {
            VerifySplitSemiColonSeparatedList("@(foo->');')", "@(foo->');')");
        }

        [Test]
        public void CloseParensInSeparator()
        {
            VerifySplitSemiColonSeparatedList("a;@(foo,');');b", "a", "@(foo,');')", "b");
        }

        [Test]
        public void CloseParensInMacroAndSeparator()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';);', ';);')", "@(foo->';);', ';);')");
        }

        [Test]
        public void EmptyQuotesInMacroAndSeparator()
        {
            VerifySplitSemiColonSeparatedList(" @(foo->'', '')", "@(foo->'', '')");
        }

        [Test]
        public void MoreParensAndAtSigns()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';());', ';@();')", "@(foo->';());', ';@();')");
        }

        [Test]
        public void SplittingExceptForMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';');def;@ghi;", "@(foo->';')", "def", "@ghi");
        }

        // Invalid item expressions shouldn't cause an error in the splitting function.
        // The caller will emit an error later when it tries to parse the results.
        [Test]
        public void InvalidItemExpressions()
        {
            VerifySplitSemiColonSeparatedList("@(x", "@(x");
            VerifySplitSemiColonSeparatedList("@(x->')", "@(x->')");
            VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
            VerifySplitSemiColonSeparatedList("@(x->''", "@(x->''");
            VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
            VerifySplitSemiColonSeparatedList("@(x->", "@(x->");
            VerifySplitSemiColonSeparatedList("@(x,')", "@(x,')");

            // This one doesn't remove the ';' because it thinks it's in
            // an item list. This isn't worth tweaking, because the invalid expression is
            // going to lead to an error in the caller whether there's a ';' or not.
            VerifySplitSemiColonSeparatedList("@(x''';", "@(x''';");
        }

        [Test]
        public void RealisticExample()
        {
            VerifySplitSemiColonSeparatedList("@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);\n                @(Compile);\n                @(ManifestResourceWithNoCulture);\n                $(ApplicationIcon);\n                $(AssemblyOriginatorKeyFile);\n                @(ManifestNonResxWithNoCultureOnDisk);\n                @(ReferencePath);\n                @(CompiledLicenseFile);\n                @(EmbeddedDocumentation);                \n                @(CustomAdditionalCompileInputs)",
                "@(_OutputPathItem->'%(FullPath)', ';')", "$(MSBuildAllProjects)", "@(Compile)", "@(ManifestResourceWithNoCulture)", "$(ApplicationIcon)", "$(AssemblyOriginatorKeyFile)", "@(ManifestNonResxWithNoCultureOnDisk)", "@(ReferencePath)", "@(CompiledLicenseFile)", "@(EmbeddedDocumentation)", "@(CustomAdditionalCompileInputs)");
        }

        // For reference, this is the authoritative definition of an item expression:
        //  @"@\(\s*
        //      (?<TYPE>[\w\x20-]*[\w-]+)
        //      (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
        //      (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
        //  \s*\)";
        // We need to support any item expressions that satisfy this expression.
        //
        // Try spaces everywhere that that regex allows spaces:
        [Test]
        public void SpacingInItemListExpression()
        {
            VerifySplitSemiColonSeparatedList("@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )", "@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )");
        }

        /// <summary>
        /// Helper method for SplitSemiColonSeparatedList tests
        /// </summary>
        /// <param name="input"></param>
        /// <param name="expected"></param>
        private void VerifySplitSemiColonSeparatedList(string input, params string[] expected)
        {
            List<string> actual = ExpressionShredder.SplitSemiColonSeparatedList(input);
            Console.WriteLine(input);

            if (null == expected)
            {
                // passing "null" means you expect an empty array back
                expected = new string[] { };
            }

            Assertion.AssertEquals("Expected " + expected.Length + " items but got " + actual.Count,
                actual.Count, expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assertion.AssertEquals(expected[i], actual[i]);
            }
        }
    }
}
