// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class FileSystemPathComparer_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void CaseVariantsAreDistinctInCaseSensitiveDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateCaseSensitiveFolder();
        string upper = Path.Combine(folder.Path, "Foo.proj");
        string lower = Path.Combine(folder.Path, "foo.proj");

        FileSystemPathComparer.Instance.Equals(upper, lower).ShouldBeFalse();
        FileSystemPathComparer.Instance.Equals(upper, new string(upper.ToCharArray())).ShouldBeTrue();
        FileSystemPathComparer.Instance.GetHashCode(upper).ShouldBe(FileSystemPathComparer.Instance.GetHashCode(lower));
    }

    [Fact]
    public void EachCaseDifferenceIsCheckedAgainstItsOwnParent()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateCaseSensitiveFolder();
        string parent = Path.GetDirectoryName(folder.Path)!;
        if (DirectoryCaseSensitivity.IsCaseSensitive(parent + Path.DirectorySeparatorChar))
        {
            Assert.Skip("Needs a case-sensitive directory inside a case-insensitive one.");
        }

        string name = Path.GetFileName(folder.Path);
        string renamed = Path.Combine(parent, name.ToUpperInvariant());
        renamed.ShouldNotBe(folder.Path);

        // The folder name is looked up in the case-insensitive parent; the file name in the case-sensitive folder.
        FileSystemPathComparer.Instance.Equals(Path.Combine(renamed, "Foo.proj"), Path.Combine(folder.Path, "Foo.proj")).ShouldBeTrue();
        FileSystemPathComparer.Instance.Equals(Path.Combine(renamed, "FOO.proj"), Path.Combine(folder.Path, "Foo.proj")).ShouldBeFalse();
    }
}
