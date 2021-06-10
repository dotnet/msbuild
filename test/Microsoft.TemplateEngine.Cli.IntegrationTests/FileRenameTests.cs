// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class FileRenameTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(VerifyTemplateContentRenames))]
        [InlineData("TestAssets.TemplateWithRenames --foo baz --testForms TestProject", "FileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithSourceName --name baz", "SourceNameFileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithUnspecifiedSourceName --name baz", "NegativeFileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomSourcePath --name bar", "CustomSourcePathRenameTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomTargetPath --name bar", "CustomTargetPathRenameTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomSourceAndTargetPaths --name bar", "CustomSourceAndTargetPathRenameTest.json")]
        [InlineData("TestAssets.TemplateWithSourcePathOutsideConfigRoot --name baz", "TemplateWithSourcePathOutsideConfigRootTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameInTargetPathGetsRenamed --name baz", "TemplateWithSourceNameInTargetPathGetsRenamedTest.json")]
        [InlineData("TestAssets.TemplateWithPlaceholderFiles", "TemplateWithPlaceholderFilesTest.json")]
        [InlineData("TestAssets.TemplateWithDerivedSymbolFileRename --name Last.Part.Is.For.Rename", "DerivedSymbolFileRenameTest.json")]
        [InlineData("TestAssets.TemplateWithMultipleRenamesOnSameFile --fooRename base --barRename ball", "MultipleRenamesOnSameFileTest.json")]
        [InlineData("TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesOverlap --fooRename pin --oobRename ball", "MultipleRenamesOnSameFileHandlesOverlapTest.json")]
        [InlineData("TestAssets.TemplateWithMultipleRenamesOnSameFileHandlesInducedOverlap --fooRename bar --barRename baz", "MultipleRenamesOnSameFileHandlesInducedOverlapTest.json")]
        [InlineData("TestAssets.TemplateWithCaseSensitiveNameBasedRenames --name NewName", "CaseSensitiveNameBasedRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithJoinAndFolderRename --name NewName --product Office", "TemplateWithJoinAndFolderRename.json")]
        public void VerifyTemplateContentRenames(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
