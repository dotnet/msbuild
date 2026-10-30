// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class AssemblyNameEx_Tests
{
    /// <summary>
    ///  Produces an <see cref="AssemblyNameExtension"/> from an assembly display name.
    /// </summary>
    /// <param name="name">The assembly display name.</param>
    /// <returns>
    ///  The <see cref="AssemblyNameExtension"/> produced from <paramref name="name"/>.
    /// </returns>
    private delegate AssemblyNameExtension ProduceAssemblyNameEx(string name);

    private static string[] s_assemblyStrings =
    {
        "System.Xml, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XML, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Version=2.0.0.0, Culture=neutral",
        "System.XM, Version=2.0.0.0",
        "System.XM, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Culture=neutral",
        "System.Xml",
        "System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Drawing",
    };

    public static TheoryData<string> AssembliesForPartialMatch { get; } =
    [
        "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes",
        "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No",
        "System.Xml, Culture=en, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=10.0.0.0, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=10.0.0.0, Culture=en",
    ];

    /// <summary>
    ///  The factory methods used to exercise each assembly-name representation.
    /// </summary>
    private static ProduceAssemblyNameEx[] s_producers =
    [
        ProduceAsString,
        ProduceAsAssemblyName,
        ProduceAsBoth,
        ProduceAsLowerString,
        ProduceAsLowerAssemblyName,
        ProduceAsLowerBoth,
    ];

    private static AssemblyNameExtension ProduceAsString(string name)
        => new(name);

    private static AssemblyNameExtension ProduceAsLowerString(string name)
        => new(name.ToLower());

    private static AssemblyNameExtension ProduceAsAssemblyName(string name)
        => new(new AssemblyName(name));

    private static AssemblyNameExtension ProduceAsLowerAssemblyName(string name)
        => new(new AssemblyName(name.ToLower()));

    private static AssemblyNameExtension ProduceAsBoth(string name)
    {
        AssemblyNameExtension result = new(new AssemblyName(name));

        // Force the string version to be produced too.
        _ = result.FullName;

        return result;
    }

    private static AssemblyNameExtension ProduceAsLowerBoth(string name)
        => ProduceAsBoth(name.ToLower());

    /// <summary>
    ///  Verifies base-name comparisons across assembly identities and representations.
    /// </summary>
    [Fact]
    public void CompareBaseName()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            AssemblyName baseName1 = new(assemblyString1);

            foreach (string assemblyString2 in s_assemblyStrings)
            {
                AssemblyName baseName2 = new(assemblyString2);

                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        a1.CompareBaseNameTo(a2)
                            .ShouldBe(string.Compare(baseName1.Name, baseName2.Name, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Verifies assembly-name ordering across identities and representations.
    /// </summary>
    [Fact]
    public void CompareTo()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            AssemblyName baseName1 = new(assemblyString1);

            foreach (string assemblyString2 in s_assemblyStrings)
            {
                AssemblyName baseName2 = new(assemblyString2);

                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        int result = a1.CompareTo(a2);

                        if (a1.Equals(a2))
                        {
                            result.ShouldBe(0);
                        }

                        if (a1.CompareBaseNameTo(a2) != 0)
                        {
                            result.ShouldBe(a1.CompareBaseNameTo(a2));
                        }

                        if (a1.CompareBaseNameTo(a2) == 0 // Only check version if basenames match
                            && a1.Version != a2.Version)
                        {
                            if (a1.Version == null)
                            {
                                // Expect -1 if a1.Version is null and the baseNames match
                                result.ShouldBe(-1);
                            }
                            else
                            {
                                result.ShouldBe(a1.Version.CompareTo(a2.Version));
                            }
                        }

                        // Only require ordering equality when the full names are equal.
                        if (string.Equals(a1.FullName, a2.FullName, StringComparison.OrdinalIgnoreCase))
                        {
                            result.ShouldBe(0);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ExerciseMiscMethods()
    {
        AssemblyNameExtension a1 = s_producers[0](s_assemblyStrings[0]);

        Version newVersion = new(1, 2);
        a1.ReplaceVersion(newVersion);
        a1.Version.ShouldBe(newVersion);

        a1.ToString().ShouldNotBeNull();
    }

    [Fact]
    public void EscapeDisplayNameCharacters()
    {
        // /// Those characters are Equals(=), Comma(,), Quote("), Apostrophe('), Backslash(\).
        string displayName = @"Hello,""Don't"" eat the \CAT";
        AssemblyNameExtension.EscapeDisplayNameCharacters(displayName).ShouldBe("""Hello\,\"Don\'t\" eat the \\CAT""", StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies equality across assembly-name representations.
    /// </summary>
    [Fact]
    public void AreEquals()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            AssemblyName baseName1 = new(assemblyString1);

            foreach (string assemblyString2 in s_assemblyStrings)
            {
                AssemblyName baseName2 = new(assemblyString2);

                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        // Baseline is a mismatch which is known to exercise
                        // the full code path.
                        AssemblyNameExtension a3 = ProduceAsAssemblyName(assemblyString1);
                        AssemblyNameExtension a4 = ProduceAsString(assemblyString2);

                        a1.Equals(a2).ShouldBe(a3.Equals(a4));
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Verifies equality across assembly-name representations while ignoring version numbers.
    /// </summary>
    [Fact]
    public void EqualsIgnoreVersion()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            AssemblyName baseName1 = new(assemblyString1);

            foreach (string assemblyString2 in s_assemblyStrings)
            {
                AssemblyName baseName2 = new(assemblyString2);

                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        // Baseline is a mismatch which is known to exercise
                        // the full code path.
                        AssemblyNameExtension a3 = ProduceAsAssemblyName(assemblyString1);
                        AssemblyNameExtension a4 = ProduceAsString(assemblyString2);

                        a1.EqualsIgnoreVersion(a2).ShouldBe(a3.EqualsIgnoreVersion(a4));
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Verifies that a fully qualified assembly name and its simple name have equal base names.
    /// </summary>
    /// <remarks>
    ///  Regression test for a bug found while implementing <see cref="AssemblyNameExtension"/>.
    /// </remarks>
    [Fact]
    public void CompareBaseNameRealCase1()
    {
        AssemblyNameExtension a1 = ProduceAsBoth("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        AssemblyNameExtension a2 = ProduceAsString("System.Drawing");

        int result = a1.CompareBaseNameTo(a2);

        // Base names should be equal.
        result.ShouldBe(0);
    }

    /// <summary>
    ///  Verifies that constructing an assembly name without a simple name throws
    ///  <see cref="FileLoadException"/>.
    /// </summary>
    [Fact]
    public void CreateAssemblyNameExtensionWithNoSimpleName()
        => Should.Throw<FileLoadException>(() =>
        {
            AssemblyNameExtension extension = new("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", true);
        });

    /// <summary>
    ///  Verifies that comparing against an assembly name without a simple name throws
    ///  <see cref="FileLoadException"/>.
    /// </summary>
    [Fact]
    public void CreateAssemblyNameExtensionWithNoSimpleName2()
        => Should.Throw<FileLoadException>(() =>
        {
            AssemblyNameExtension extension = new AssemblyNameExtension("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension extension2 = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            extension2.PartialNameCompare(extension);
        });

    /// <summary>
    ///  Verifies <see cref="AssemblyNameExtension"/> construction with optional version, culture,
    ///  and public-key token information, including cases where the public-key token is the only qualifier.
    /// </summary>
    [Theory]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", "2.0.0.0", "en", "b03f5f7f11d50a3a")]
    [InlineData("A, Version=2.0.0.0, PublicKeyToken=b03f5f7f11d50a3a", "2.0.0.0", null, "b03f5f7f11d50a3a")]
    [InlineData("A, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", null, "en", "b03f5f7f11d50a3a")]
    [InlineData("A, PublicKeyToken=b03f5f7f11d50a3a", null, null, "b03f5f7f11d50a3a")]
    [InlineData("A", null, null, null)]
    public void CreateAssemblyNameWithNameAndVersionCulturePublicKey(
        string assemblyName, string? expectedVersion, string? expectedCulture, string? expectedPublicKeyToken)
    {
        AssemblyNameExtension extension = new(assemblyName);
        extension.Name.ShouldBe("A");
        extension.Version.ShouldBe(expectedVersion is null ? null : new Version(expectedVersion));
        extension.CultureInfo.ShouldBe(expectedCulture is null ? null : new CultureInfo(expectedCulture));

        if (expectedPublicKeyToken is not null)
        {
            extension.FullName.ShouldContain(expectedPublicKeyToken);
        }
    }

    /// <summary>
    ///  Verifies processor-architecture detection in assembly display names.
    /// </summary>
    [Theory]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, ProcessorArchitecture=MSIL", true)]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", false)]
    public void CreateAssemblyNameWithNameAndProcessorArchitecture(string assemblyName, bool hasProcessorArchitecture)
    {
        AssemblyNameExtension extension = new(assemblyName);
        extension.Name.ShouldBe("A");
        extension.Version.ShouldBe(new Version("2.0.0.0"));
        extension.CultureInfo.ShouldBe(new CultureInfo("en"));
        extension.FullName.ShouldContain("b03f5f7f11d50a3a");
        extension.HasProcessorArchitectureInFusionName.ShouldBe(hasProcessorArchitecture);

        if (hasProcessorArchitecture)
        {
            extension.FullName.ShouldContain("MSIL");
        }
    }

    /// <summary>
    ///  Verifies partial matching by simple name.
    /// </summary>
    [Theory]
    [MemberData(nameof(AssembliesForPartialMatch))]
    public void TestAssemblyPartialMatchSimpleName(string assembly)
    {
        AssemblyNameExtension assemblyNameToMatch = new("System.Xml");
        AssemblyNameExtension assemblyNameToNotMatch = new("System.Xmla");

        AssemblyNameExtension assemblyToCompare = new(assembly);

        assemblyNameToMatch.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
        assemblyNameToMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName).ShouldBeTrue();
        assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
        assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies partial matching by simple name and version.
    /// </summary>
    [Theory]
    [MemberData(nameof(AssembliesForPartialMatch))]
    public void TestAssemblyPartialMatchSimpleNameVersion(string assembly)
    {
        AssemblyNameExtension assemblyNameToMatchVersion = new("System.Xml, Version=10.0.0.0");
        AssemblyNameExtension assemblyNameToNotMatch = new("System.Xml, Version=5.0.0.0");
        AssemblyNameExtension assemblyMatchNoVersion = new("System.Xml");

        AssemblyNameExtension assemblyToCompare = new(assembly);

        // If there is a version make sure the assembly name with the correct version matches
        // Make sure the assembly with the wrong version does not match
        if (assemblyToCompare.Version is not null)
        {
            assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeTrue();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeFalse();

            // Matches because version is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeTrue();
        }
        else
        {
            // If there is no version make names with a version specified do not match
            assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeFalse();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeFalse();

            // Matches because version is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version).ShouldBeTrue();
        }
    }

    /// <summary>
    ///  Verifies partial matching by simple name and culture.
    /// </summary>
    [Theory]
    [MemberData(nameof(AssembliesForPartialMatch))]
    public void TestAssemblyPartialMatchSimpleNameCulture(string assembly)
    {
        AssemblyNameExtension assemblyNameToMatchCulture = new("System.Xml, Culture=en");
        AssemblyNameExtension assemblyNameToNotMatch = new("System.Xml, Culture=de-DE");
        AssemblyNameExtension assemblyMatchNoVersion = new("System.Xml");

        AssemblyNameExtension assemblyToCompare = new(assembly);

        // If there is a version make sure the assembly name with the correct culture matches
        // Make sure the assembly with the wrong culture does not match
        if (assemblyToCompare.CultureInfo is not null)
        {
            assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeTrue();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeFalse();

            // Matches because culture is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeTrue();
        }
        else
        {
            // If there is no version make names with a culture specified do not match
            assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeFalse();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeFalse();

            // Matches because culture is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture).ShouldBeTrue();
        }
    }

    /// <summary>
    ///  Verifies partial matching by simple name and public-key token.
    /// </summary>
    [Theory]
    [MemberData(nameof(AssembliesForPartialMatch))]
    public void TestAssemblyPartialMatchSimpleNamePublicKeyToken(string assembly)
    {
        AssemblyNameExtension assemblyNameToMatchPublicToken = new("System.Xml, PublicKeyToken=b03f5f7f11d50a3a");
        AssemblyNameExtension assemblyNameToNotMatch = new("System.Xml, PublicKeyToken=b03f5f7f11d50a3b");
        AssemblyNameExtension assemblyMatchNoVersion = new("System.Xml");

        AssemblyNameExtension assemblyToCompare = new(assembly);

        // If there is a version make sure the assembly name with the correct publicKeyToken matches
        // Make sure the assembly with the wrong publicKeyToken does not match
        if (assemblyToCompare.GetPublicKeyToken() is not null)
        {
            assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeTrue();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeFalse();

            // Matches because publicKeyToken is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeTrue();
        }
        else
        {
            // If there is no version make names with a publicKeyToken specified do not match
            assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeFalse();

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBeFalse();
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeFalse();

            // Matches because publicKeyToken is not specified
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken).ShouldBeTrue();
        }
    }

    /// <summary>
    ///  Verifies partial matching by simple name with and without considering the retargetable flag.
    /// </summary>
    [Theory]
    [MemberData(nameof(AssembliesForPartialMatch))]
    public void TestAssemblyPartialMatchSimpleNameRetargetable(string assembly)
    {
        AssemblyNameExtension assemblyNameToMatchRetargetable = new("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
        AssemblyNameExtension assemblyNameToNotMatch = new("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
        AssemblyNameExtension assemblyMatchNoRetargetable = new("System.Xml");

        AssemblyNameExtension assemblyToCompare = new(assembly);

        if (assemblyToCompare.FullName.IndexOf("Retargetable=Yes", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true).ShouldBeTrue();

            assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch).ShouldBeTrue();
            assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch, PartialComparisonFlags.SimpleName, true).ShouldBeFalse();

            assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable).ShouldBeFalse();
            assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable, PartialComparisonFlags.SimpleName, true).ShouldBeFalse();

            assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true).ShouldBeFalse();
        }
        else
        {
            assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true).ShouldBeFalse();

            // Match because retargetable false is the same as no retargetable bit
            bool expectedMatch = assemblyToCompare.FullName.IndexOf(
                "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a",
                StringComparison.OrdinalIgnoreCase) >= 0;
            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare).ShouldBe(expectedMatch);

            assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true).ShouldBeTrue();

            assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare).ShouldBeTrue();
            assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true).ShouldBeTrue();
        }
    }

    /// <summary>
    ///  Verifies assembly-name equality with and without considering the retargetable flag.
    /// </summary>
    [Theory]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", false, true)]
    [InlineData("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", false, false)]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", true, false)]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes", true, true)]
    [InlineData("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", true, false)]
    public void VerifyAssemblyNameEqualityComparers(string comparedName, bool considerRetargetable, bool expected)
    {
        AssemblyNameExtension assemblyName = new("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
        AssemblyNameExtension comparedAssemblyName = new(comparedName);
        IEqualityComparer<AssemblyNameExtension> comparer = considerRetargetable
            ? AssemblyNameComparer.GenericComparerConsiderRetargetable
            : AssemblyNameComparer.GenericComparer;

        comparer.Equals(assemblyName, comparedAssemblyName).ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies assembly-name ordering with and without considering the retargetable flag.
    /// </summary>
    [Theory]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", false, 0)]
    [InlineData("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", false, 1)]
    [InlineData("System.Xml, Version=11.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", false, -1)]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes", true, 0)]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", true, 1)]
    [InlineData("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", true, 1)]
    [InlineData("System.Xml, Version=11.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No", true, -1)]
    public void VerifyAssemblyNameOrderingComparers(string comparedName, bool considerRetargetable, int expectedSign)
    {
        AssemblyNameExtension assemblyName = new("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
        AssemblyNameExtension comparedAssemblyName = new(comparedName);
        IComparer comparer = considerRetargetable
            ? AssemblyNameComparer.ComparerConsiderRetargetable
            : AssemblyNameComparer.Comparer;

        Math.Sign(comparer.Compare(assemblyName, comparedAssemblyName)).ShouldBe(expectedSign);
    }

    /// <summary>
    ///  Verifies reverse-version ordering, including equal versions, <see langword="null"/> values,
    ///  and different simple names.
    /// </summary>
    [Theory]
    [InlineData("System, Version=2.0.0.0", "System, Version=1.0.0.0", -1)]
    [InlineData("System, Version=1.0.0.0", "System, Version=2.0.0.0", 1)]
    [InlineData("System, Version=2.0.0.0", "System, Version=2.0.0.0", 0)]
    [InlineData(null, null, 0)]
    [InlineData("System, Version=2.0.0.0", null, -1)]
    [InlineData(null, "System, Version=1.0.0.0", 1)]
    [InlineData("Zar, Version=3.0.0.0", "System, Version=2.0.0.0", -1)]
    public void VerifyReverseVersionComparer(string? leftName, string? rightName, int expected)
    {
        AssemblyNameExtension? left = leftName is null ? null : new(leftName);
        AssemblyNameExtension? right = rightName is null ? null : new(rightName);
        AssemblyNameReverseVersionComparer reverseComparer = new();

        reverseComparer.Compare(left, right).ShouldBe(expected);
    }

    [Fact]
    public void SortWithReverseVersionComparer()
    {
        AssemblyNameExtension x = new("System, Version=2.0.0.0");
        AssemblyNameExtension y = new("System, Version=1.0.0.0");
        AssemblyNameExtension z = new("System, Version=2.0.0.0");

        List<AssemblyNameExtension> assemblies = [y, x, z];

        assemblies.Sort(AssemblyNameReverseVersionComparer.GenericComparer);

        assemblies[0].ShouldBe(x);
        assemblies[1].ShouldBe(z);
        assemblies[2].ShouldBe(y);
    }

    [Theory]
    [InlineData("System.Xml")]
    [InlineData("System.XML, Version=2.0.0.0")]
    [InlineData("System.Xml, Culture=de-DE")]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes")]
    [InlineData("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public void VerifyAssemblyNameExSerializationByTranslator(string assemblyName)
    {
        AssemblyNameExtension assemblyNameOriginal = new(assemblyName);
        AssemblyNameExtension? assemblyNameDeserialized = null;

        using MemoryStream serializationStream = new();
        ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(serializationStream);

        writeTranslator.Translate(ref assemblyNameOriginal, (ITranslator t) => new AssemblyNameExtension(t));

        serializationStream.Seek(0, SeekOrigin.Begin);
        ITranslator readTranslator = BinaryTranslator.GetReadTranslator(serializationStream, InterningBinaryReader.PoolingBuffer);

        readTranslator.Translate(ref assemblyNameDeserialized, (ITranslator t) => new AssemblyNameExtension(t));

        assemblyNameDeserialized.ShouldBe(assemblyNameOriginal);
    }

    [Fact]
    public void VerifyAssemblyNameExSerializationWithRemappedFromByTranslator()
    {
        AssemblyNameExtension assemblyNameOriginal = new("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
        AssemblyNameExtension assemblyRemappedFrom = new("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
        assemblyRemappedFrom.MarkImmutable();
        assemblyNameOriginal.AddRemappedAssemblyName(assemblyRemappedFrom);
        assemblyNameOriginal.RemappedFromEnumerator.Count().ShouldBe(1);

        AssemblyNameExtension? assemblyNameDeserialized = null;

        using MemoryStream serializationStream = new();
        ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(serializationStream);

        writeTranslator.Translate(ref assemblyNameOriginal, (ITranslator t) => new AssemblyNameExtension(t));

        serializationStream.Seek(0, SeekOrigin.Begin);
        ITranslator readTranslator = BinaryTranslator.GetReadTranslator(serializationStream, InterningBinaryReader.PoolingBuffer);

        readTranslator.Translate(ref assemblyNameDeserialized, (ITranslator t) => new AssemblyNameExtension(t));

        assemblyNameDeserialized.ShouldNotBeNull().Equals(assemblyNameOriginal).ShouldBeTrue();
        assemblyNameDeserialized.RemappedFromEnumerator.Count().ShouldBe(1);
        assemblyNameDeserialized.RemappedFromEnumerator.First().ShouldBe(assemblyRemappedFrom);
    }
}
