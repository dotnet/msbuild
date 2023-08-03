// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToIncludeItemsOutsideTheProjectFolder : SdkTest
    {
        public GivenThatWeWantToIncludeItemsOutsideTheProjectFolder(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Link_metadata_is_added_to_items_outside_the_project_folder(bool includeWithGlob, bool useLinkBase)
        {
            string identifier = (includeWithGlob ? "Globbed" : "Direct") + (useLinkBase ? "_LinkBase" : "");
            var testAsset = _testAssetsManager
                .CopyTestAsset("LinkTest", "LinkTest_", identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Element(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement(ns + "IncludeWithGlob", includeWithGlob));
                    propertyGroup.Add(new XElement(ns + "UseLinkBase", useLinkBase));
                });

            var command = new MSBuildCommand(Log, "WriteItems", testAsset.TestRoot, "LinkTest");

            command.Execute()
                .Should()
                .Pass();

            string intermediateOutputPath = Path.Combine(command.GetBaseIntermediateDirectory().FullName, "Debug", "netstandard2.0");
            string itemsFile = Path.Combine(intermediateOutputPath, "Items.txt");

            var items = File.ReadAllLines(itemsFile)
                .Select(l => l.Split('\t'))
                .Select(f => (itemType: f[0], fullPath: f[1], link: f[2]))
                .ToList();

            var itemDict = items.GroupBy(i => i.itemType)
                .ToDictionary(g => g.Key, g => g.Select(i => (fullPath: i.fullPath, link: i.link)).ToList());

            //  Remove generated source files
            itemDict["Compile"].RemoveAll(i =>
            {
                string filename = Path.GetFileName(i.fullPath);
                return filename.Contains("AssemblyInfo") ||
                        filename.Contains("AssemblyAttributes");
            });

            var expectedItems = new Dictionary<string, List<string>>()
            {
                ["Compile"] = new List<string>() { "Class1.cs", @"..\Linked\Linked.Class.cs" },
                ["AdditionalFiles"] = new List<string>() { @"..\Linked\Linked.Additional.txt" },
                ["None"] = new List<string>() { @"..\Linked\Linked.None.txt" },
                ["Content"] = new List<string>() { @"..\Linked\Linked.Content.txt" },
                ["EmbeddedResource"] = new List<string>() { @"..\Linked\Linked.Embedded.txt" },
                ["Page"] = new List<string>() { @"..\Linked\Linked.page.xaml" },
                ["Resource"] = new List<string>() { @"..\Linked\Linked.resource.xaml" },
                ["CustomItem"] = new List<string>() { @"..\Linked\Linked.Custom.txt" },
            };

            if (includeWithGlob)
            {
                expectedItems["Compile"].Add(@"..\Linked\A\B C\Linked.Class.cs");
                expectedItems["AdditionalFiles"].Add(@"..\Linked\A\B C\Linked.Additional.txt");
                expectedItems["None"].Add(@"..\Linked\A\B C\Linked.None.txt");
                expectedItems["Content"].Add(@"..\Linked\A\B C\Linked.Content.txt");
                expectedItems["EmbeddedResource"].Add(@"..\Linked\A\B C\Linked.Embedded.txt");
                expectedItems["Page"].Add(@"..\Linked\A\B C\Linked.page.xaml");
                expectedItems["Resource"].Add(@"..\Linked\A\B C\Linked.resource.xaml");
                expectedItems["CustomItem"].Add(@"..\Linked\A\B C\Linked.Custom.txt");
            }

            var projectFolder = Path.Combine(testAsset.TestRoot, "LinkTest");

            var expectedItemMetadata = expectedItems.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(item =>
                {
                    string fullPath = Path.GetFullPath(Path.Combine(projectFolder, item.Replace('\\', Path.DirectorySeparatorChar)));
                    string link = "";
                    string linkedPrefix = @"..\Linked\";
                    if (item.StartsWith(linkedPrefix) && kvp.Key != "CustomItem")
                    {
                        link = item.Substring(linkedPrefix.Length);
                        if (useLinkBase)
                        {
                            link = @"Linked\Files\" + link;
                        }
                    }

                    link = link.Replace('\\', Path.DirectorySeparatorChar);

                    return (fullPath: fullPath, link: link);
                }));

            foreach (var itemType in expectedItemMetadata.Keys)
            {
                itemDict[itemType].Should().BeEquivalentTo(expectedItemMetadata[itemType]);
            }
        }
    }
}
