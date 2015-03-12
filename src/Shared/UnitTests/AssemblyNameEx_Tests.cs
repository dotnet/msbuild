// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class AssemblyNameEx_Tests
    {
        /// <summary>
        /// Delegate defines a function that produces an AssemblyNameExtension from a string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal delegate AssemblyNameExtension ProduceAssemblyNameEx(string name);

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
            "System.Drawing"
        };

        private static string[] s_assembliesForPartialMatch =
        {
            "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes",
            "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No",
            "System.Xml, Culture=en, PublicKeyToken=b03f5f7f11d50a3a",
            "System.Xml, Version=10.0.0.0, PublicKeyToken=b03f5f7f11d50a3a",
            "System.Xml, Version=10.0.0.0, Culture=en"
        };

        /// <summary>
        /// All the different ways the same assembly name can be represented.
        /// </summary>
        private static ProduceAssemblyNameEx[] s_producers =
        {
            new ProduceAssemblyNameEx(ProduceAsString),
            new ProduceAssemblyNameEx(ProduceAsAssemblyName),
            new ProduceAssemblyNameEx(ProduceAsBoth),
            new ProduceAssemblyNameEx(ProduceAsLowerString),
            new ProduceAssemblyNameEx(ProduceAsLowerAssemblyName),
            new ProduceAssemblyNameEx(ProduceAsLowerBoth)
        };



        private static AssemblyNameExtension ProduceAsString(string name)
        {
            return new AssemblyNameExtension(name);
        }

        private static AssemblyNameExtension ProduceAsLowerString(string name)
        {
            return new AssemblyNameExtension(name.ToLower());
        }

        private static AssemblyNameExtension ProduceAsAssemblyName(string name)
        {
            return new AssemblyNameExtension(new AssemblyName(name));
        }

        private static AssemblyNameExtension ProduceAsLowerAssemblyName(string name)
        {
            return new AssemblyNameExtension(new AssemblyName(name.ToLower()));
        }

        private static AssemblyNameExtension ProduceAsBoth(string name)
        {
            AssemblyNameExtension result = new AssemblyNameExtension(new AssemblyName(name));

            // Force the string version to be produced too.
            string backToString = result.FullName;

            return result;
        }

        private static AssemblyNameExtension ProduceAsLowerBoth(string name)
        {
            return ProduceAsBoth(name.ToLower());
        }

        /// <summary>
        /// General base name comparison validator.
        /// </summary>
        [TestMethod]
        public void CompareBaseName()
        {
            // For each pair of assembly strings...
            foreach (string assemblyString1 in s_assemblyStrings)
            {
                AssemblyName baseName1 = new AssemblyName(assemblyString1);

                foreach (string assemblyString2 in s_assemblyStrings)
                {
                    AssemblyName baseName2 = new AssemblyName(assemblyString2);

                    // ...and for each pair of production methods...
                    foreach (ProduceAssemblyNameEx produce1 in s_producers)
                    {
                        foreach (ProduceAssemblyNameEx produce2 in s_producers)
                        {
                            AssemblyNameExtension a1 = produce1(assemblyString1);
                            AssemblyNameExtension a2 = produce2(assemblyString2);

                            int result = a1.CompareBaseNameTo(a2);
                            int resultBaseline = String.Compare(baseName1.Name, baseName2.Name, StringComparison.OrdinalIgnoreCase);
                            if (resultBaseline != result)
                            {
                                Assert.AreEqual(resultBaseline, result);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// General compareTo validator
        /// </summary>
        [TestMethod]
        public void CompareTo()
        {
            // For each pair of assembly strings...
            foreach (string assemblyString1 in s_assemblyStrings)
            {
                AssemblyName baseName1 = new AssemblyName(assemblyString1);

                foreach (string assemblyString2 in s_assemblyStrings)
                {
                    AssemblyName baseName2 = new AssemblyName(assemblyString2);

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
                                Assert.AreEqual(0, result);
                            }

                            if (a1.CompareBaseNameTo(a2) != 0)
                            {
                                Assert.AreEqual(a1.CompareBaseNameTo(a2), result);
                            }

                            if
                                (
                                    a1.CompareBaseNameTo(a2) == 0   // Only check version if basenames match
                                    && a1.Version != a2.Version
                                )
                            {
                                if (a1.Version == null)
                                {
                                    // Expect -1 if a1.Version is null and the baseNames match
                                    Assert.AreEqual(-1, result);
                                }
                                else
                                {
                                    Assert.AreEqual(a1.Version.CompareTo(a2.Version), result);
                                }
                            }

                            int resultBaseline = String.Compare(a1.FullName, a2.FullName, StringComparison.OrdinalIgnoreCase);
                            // Only check to see if the result and the resultBaseline match when the result baseline is 0 and the result is not 0.
                            if (resultBaseline != result && resultBaseline == 0)
                            {
                                Assert.AreEqual(resultBaseline, result);
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void ExerciseMiscMethods()
        {
            AssemblyNameExtension a1 = s_producers[0](s_assemblyStrings[0]);
            Assert.IsNotNull(a1.GetHashCode());

            Version newVersion = new Version(1, 2);
            a1.ReplaceVersion(newVersion);
            Assert.IsTrue(a1.Version.Equals(newVersion));

            Assert.IsNotNull(a1.ToString());
        }

        [TestMethod]
        public void EscapeDisplayNameCharacters()
        {
            // /// Those characters are Equals(=), Comma(,), Quote("), Apostrophe('), Backslash(\).
            string displayName = @"Hello,""Don't"" eat the \CAT";
            Assert.IsTrue(String.Compare(AssemblyNameExtension.EscapeDisplayNameCharacters(displayName), @"Hello\,\""Don\'t\"" eat the \\CAT", StringComparison.OrdinalIgnoreCase) == 0);
        }


        /// <summary>
        /// General equals comparison validator.
        /// </summary>
        [TestMethod]
        public void Equals()
        {
            // For each pair of assembly strings...
            foreach (string assemblyString1 in s_assemblyStrings)
            {
                AssemblyName baseName1 = new AssemblyName(assemblyString1);

                foreach (string assemblyString2 in s_assemblyStrings)
                {
                    AssemblyName baseName2 = new AssemblyName(assemblyString2);

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

                            bool result = a1.Equals(a2);
                            bool resultBaseline = a3.Equals(a4);
                            if (result != resultBaseline)
                            {
                                Assert.AreEqual(resultBaseline, result);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// General equals comparison validator when we are ignoring the version numbers in the name.
        /// </summary>
        [TestMethod]
        public void EqualsIgnoreVersion()
        {
            // For each pair of assembly strings...
            foreach (string assemblyString1 in s_assemblyStrings)
            {
                AssemblyName baseName1 = new AssemblyName(assemblyString1);

                foreach (string assemblyString2 in s_assemblyStrings)
                {
                    AssemblyName baseName2 = new AssemblyName(assemblyString2);

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

                            bool result = a1.EqualsIgnoreVersion(a2);
                            bool resultBaseline = a3.EqualsIgnoreVersion(a4);
                            if (result != resultBaseline)
                            {
                                Assert.AreEqual(resultBaseline, result);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This repros a bug that was found while coding AssemblyNameExtension.
        /// </summary>
        [TestMethod]
        public void CompareBaseNameRealCase1()
        {
            AssemblyNameExtension a1 = ProduceAsBoth("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension a2 = ProduceAsString("System.Drawing");

            int result = a1.CompareBaseNameTo(a2);

            // Base names should be equal.
            Assert.AreEqual(0, result);
        }

        /// <summary>
        /// Verify an exception is thrown when the simple name is not in the itemspec.
        /// 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(FileLoadException))]
        public void CreateAssemblyNameExtensionWithNoSimpleName()
        {
            AssemblyNameExtension extension = new AssemblyNameExtension("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", true);
        }

        /// <summary>
        /// Verify an exception is thrown when the simple name is not in the itemspec.
        /// 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(FileLoadException))]
        public void CreateAssemblyNameExtensionWithNoSimpleName2()
        {
            AssemblyNameExtension extension = new AssemblyNameExtension("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension extension2 = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            extension2.PartialNameCompare(extension);
        }

        /// <summary>
        /// Create an assembly name extension providing the name, version, culture, and public key. Also test cases
        /// where the public key is the only item specified
        /// </summary>
        [TestMethod]
        public void CreateAssemblyNameWithNameAndVersionCulturePublicKey()
        {
            AssemblyNameExtension extension = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(extension.Version.Equals(new Version("2.0.0.0")));
            Assert.IsTrue(extension.CultureInfo.Equals(CultureInfo.GetCultureInfo("en")));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));

            extension = new AssemblyNameExtension("A, Version=2.0.0.0, PublicKeyToken=b03f5f7f11d50a3a");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(extension.Version.Equals(new Version("2.0.0.0")));
            Assert.IsTrue(Object.ReferenceEquals(extension.CultureInfo, null));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));

            extension = new AssemblyNameExtension("A, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(Object.ReferenceEquals(extension.Version, null));
            Assert.IsTrue(extension.CultureInfo.Equals(CultureInfo.GetCultureInfo("en")));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));

            extension = new AssemblyNameExtension("A, PublicKeyToken=b03f5f7f11d50a3a");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(Object.ReferenceEquals(extension.Version, null));
            Assert.IsTrue(Object.ReferenceEquals(extension.CultureInfo, null));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));

            extension = new AssemblyNameExtension("A");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(Object.ReferenceEquals(extension.Version, null));
            Assert.IsTrue(Object.ReferenceEquals(extension.CultureInfo, null));
        }

        /// <summary>
        /// Make sure processor architecture is seen when it is in the string.
        /// </summary>
        [TestMethod]
        public void CreateAssemblyNameWithNameAndProcessorArchitecture()
        {
            AssemblyNameExtension extension = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, ProcessorArchitecture=MSIL");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(extension.Version.Equals(new Version("2.0.0.0")));
            Assert.IsTrue(extension.CultureInfo.Equals(CultureInfo.GetCultureInfo("en")));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));
            Assert.IsTrue(extension.FullName.Contains("MSIL"));
            Assert.IsTrue(extension.HasProcessorArchitectureInFusionName);

            extension = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            Assert.IsTrue(extension.Name.Equals("A", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(extension.Version.Equals(new Version("2.0.0.0")));
            Assert.IsTrue(extension.CultureInfo.Equals(CultureInfo.GetCultureInfo("en")));
            Assert.IsTrue(extension.FullName.Contains("b03f5f7f11d50a3a"));
            Assert.IsFalse(extension.HasProcessorArchitectureInFusionName);
        }


        /// <summary>
        /// Verify partial matching on the simple name works
        /// </summary>
        [TestMethod]
        public void TestAssemblyPatialMatchSimpleName()
        {
            AssemblyNameExtension assemblyNameToMatch = new AssemblyNameExtension("System.Xml");
            AssemblyNameExtension assemblyNameToNotMatch = new AssemblyNameExtension("System.Xmla");

            foreach (string assembly in s_assembliesForPartialMatch)
            {
                AssemblyNameExtension assemblyToCompare = new AssemblyNameExtension(assembly);

                Assert.IsTrue(assemblyNameToMatch.PartialNameCompare(assemblyToCompare));
                Assert.IsTrue(assemblyNameToMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName));
                Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName));
            }
        }

        /// <summary>
        /// Verify partial matching on the simple name and version
        /// </summary>
        [TestMethod]
        public void TestAssemblyPatialMatchSimpleNameVersion()
        {
            AssemblyNameExtension assemblyNameToMatchVersion = new AssemblyNameExtension("System.Xml, Version=10.0.0.0");
            AssemblyNameExtension assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Version=5.0.0.0");
            AssemblyNameExtension assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

            foreach (string assembly in s_assembliesForPartialMatch)
            {
                AssemblyNameExtension assemblyToCompare = new AssemblyNameExtension(assembly);

                // If there is a version make sure the assembly name with the correct version matches
                // Make sure the assembly with the wrong version does not match
                if (assemblyToCompare.Version != null)
                {
                    Assert.IsTrue(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                    // Matches because version is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));
                }
                else
                {
                    // If there is no version make names with a version specified do not match
                    Assert.IsFalse(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                    // Matches because version is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));
                }
            }
        }

        /// <summary>
        /// Verify partial matching on the simple name and culture
        /// </summary>
        [TestMethod]
        public void TestAssemblyPatialMatchSimpleNameCulture()
        {
            AssemblyNameExtension assemblyNameToMatchCulture = new AssemblyNameExtension("System.Xml, Culture=en");
            AssemblyNameExtension assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Culture=de-DE");
            AssemblyNameExtension assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

            foreach (string assembly in s_assembliesForPartialMatch)
            {
                AssemblyNameExtension assemblyToCompare = new AssemblyNameExtension(assembly);

                // If there is a version make sure the assembly name with the correct culture matches
                // Make sure the assembly with the wrong culture does not match
                if (assemblyToCompare.CultureInfo != null)
                {
                    Assert.IsTrue(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                    // Matches because culture is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));
                }
                else
                {
                    // If there is no version make names with a culture specified do not match
                    Assert.IsFalse(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                    // Matches because culture is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));
                }
            }
        }

        /// <summary>
        /// Verify partial matching on the simple name and PublicKeyToken
        /// </summary>
        [TestMethod]
        public void TestAssemblyPatialMatchSimpleNamePublicKeyToken()
        {
            AssemblyNameExtension assemblyNameToMatchPublicToken = new AssemblyNameExtension("System.Xml, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, PublicKeyToken=b03f5f7f11d50a3b");
            AssemblyNameExtension assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

            foreach (string assembly in s_assembliesForPartialMatch)
            {
                AssemblyNameExtension assemblyToCompare = new AssemblyNameExtension(assembly);

                // If there is a version make sure the assembly name with the correct publicKeyToken matches
                // Make sure the assembly with the wrong publicKeyToken does not match
                if (assemblyToCompare.GetPublicKeyToken() != null)
                {
                    Assert.IsTrue(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                    // Matches because publicKeyToken is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));
                }
                else
                {
                    // If there is no version make names with a publicKeyToken specified do not match
                    Assert.IsFalse(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                    // Matches because publicKeyToken is not specified
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));
                }
            }
        }

        /// <summary>
        /// Verify partial matching on the simple name and retargetable
        /// </summary>
        [TestMethod]
        public void TestAssemblyPartialMatchSimpleNameRetargetable()
        {
            AssemblyNameExtension assemblyNameToMatchRetargetable = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
            AssemblyNameExtension assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
            AssemblyNameExtension assemblyMatchNoRetargetable = new AssemblyNameExtension("System.Xml");

            foreach (string assembly in s_assembliesForPartialMatch)
            {
                AssemblyNameExtension assemblyToCompare = new AssemblyNameExtension(assembly);

                if (assemblyToCompare.FullName.IndexOf("Retargetable=Yes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Assert.IsTrue(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                    Assert.IsTrue(assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch));
                    Assert.IsFalse(assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch, PartialComparisonFlags.SimpleName, true));

                    Assert.IsFalse(assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable));
                    Assert.IsFalse(assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable, PartialComparisonFlags.SimpleName, true));

                    Assert.IsTrue(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare));
                    Assert.IsFalse(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));
                }
                else
                {
                    Assert.IsFalse(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                    // Match because retargetable false is the same as no retargetable bit
                    bool match = assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare);
                    if (assemblyToCompare.FullName.IndexOf("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Assert.IsTrue(match);
                    }
                    else
                    {
                        Assert.IsFalse(match);
                    }
                    Assert.IsTrue(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                    Assert.IsTrue(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare));
                    Assert.IsTrue(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));
                }
            }
        }


        /// <summary>
        /// Make sure that our assemblyNameComparers correctly work.
        /// </summary>
        [TestMethod]
        public void VerifyAssemblyNameComparers()
        {
            AssemblyNameExtension a = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
            AssemblyNameExtension b = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
            AssemblyNameExtension c = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");

            AssemblyNameExtension d = new AssemblyNameExtension("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
            AssemblyNameExtension e = new AssemblyNameExtension("System.Xml, Version=11.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");

            Assert.IsTrue(AssemblyNameComparer.GenericComparer.Equals(a, b));
            Assert.IsFalse(AssemblyNameComparer.GenericComparer.Equals(a, d));

            Assert.IsFalse(AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, b));
            Assert.IsTrue(AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, c));
            Assert.IsFalse(AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, d));


            Assert.IsTrue(AssemblyNameComparer.Comparer.Compare(a, b) == 0);
            Assert.IsTrue(AssemblyNameComparer.Comparer.Compare(a, d) > 0);
            Assert.IsTrue(AssemblyNameComparer.Comparer.Compare(a, e) < 0);

            Assert.IsTrue(AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, c) == 0);
            Assert.IsTrue(AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, b) > 0);
            Assert.IsTrue(AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, d) > 0);
            Assert.IsTrue(AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, e) < 0);
        }


        /// <summary>
        /// Make sure the reverse version comparer will compare the version in a way that would sort them in reverse order. 
        /// </summary>
        [TestMethod]
        public void VerifyReverseVersionComparer()
        {
            AssemblyNameExtension x = new AssemblyNameExtension("System, Version=2.0.0.0");
            AssemblyNameExtension y = new AssemblyNameExtension("System, Version=1.0.0.0");
            AssemblyNameExtension z = new AssemblyNameExtension("System, Version=2.0.0.0");
            AssemblyNameExtension a = new AssemblyNameExtension("Zar, Version=3.0.0.0");

            AssemblyNameReverseVersionComparer reverseComparer = new AssemblyNameReverseVersionComparer();
            Assert.AreEqual(-1, reverseComparer.Compare(x, y));
            Assert.AreEqual(1, reverseComparer.Compare(y, x));
            Assert.AreEqual(0, reverseComparer.Compare(x, z));
            Assert.AreEqual(0, reverseComparer.Compare(null, null));
            Assert.AreEqual(-1, reverseComparer.Compare(x, null));
            Assert.AreEqual(1, reverseComparer.Compare(null, y));
            Assert.AreEqual(-1, reverseComparer.Compare(a, x));

            List<AssemblyNameExtension> assemblies = new List<AssemblyNameExtension>();
            assemblies.Add(y);
            assemblies.Add(x);
            assemblies.Add(z);

            assemblies.Sort(AssemblyNameReverseVersionComparer.GenericComparer);

            Assert.IsTrue(assemblies[0].Equals(x));
            Assert.IsTrue(assemblies[1].Equals(z));
            Assert.IsTrue(assemblies[2].Equals(y));
        }
    }
}




