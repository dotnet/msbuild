// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class VisualBasicParserUtilititesTests
    {
        // Try just and empty file
        [Fact]
        public void EmptyFile()
        {
            AssertParse("", null);
        }

        // A simple form
        [Fact]
        public void SimpleForm()
        {
            AssertParse
            (
                @"
        rem This is my class
        <DefaultInstanceProperty(&qtGetInstance&qt)> Public ClAsS Form1
        End Class
                ",
                "Form1");
        }

        // A simple form with a namespace
        [Fact]
        public void Namespace()
        {
            AssertParse
            (
                @"
    ' This is my Namespace
    NamEspacE Goofy.Mickey
        rem This is my class
        <DefaultInstanceProperty(&qtGetInstance&qt)> Public ClAsS Form1
        End Class
    End Namespace
                ",
                "Goofy.Mickey.Form1");
        }

        // A simple form with a namespace
        [Fact]
        public void NestedNamespace()
        {
            AssertParse
            (
                @"
    Namespace Goofy
        Namespace Mickey
            <DefaultInstanceProperty(&qtGetInstance&qt)> Public Class Form1      
                ",
                "Goofy.Mickey.Form1");
        }

        // A namespace the is ended before the class
        [Fact]
        public void NestedAndEndedNamespace()
        {
            AssertParse
            (
                @"
    Namespace Goofy
        Namespace Mickey
        End Namespace ' Just finished with the namespace, about to make a class
        <DefaultInstanceProperty(&qtthis propert is a class name&qt)> PuBlic Class Form1      
                ",
                "Goofy.Form1");
        }

        /// <summary>
        /// Our Visual Basic parser would sink any string that begins with "rem" as
        /// the beginning of a comment, even if the "rem" wasn't immediately followed
        /// by whitespace.  This resulted in broken resource names when the namespace
        /// name was something like "BugResources.RemoveStuff.XYZ", because we would
        /// only match the "BugResources" bit.
        /// </summary>
        [Fact]
        public void NamespaceElementBeginsWithRem()
        {
            AssertParse
(
    @"
    ' This is my Namespace
    NamEspacE Artist.Painter.Rembrandt
        rem This is my class
        <DefaultInstanceProperty(&qtGetInstance&qt)> Public ClAsS SelfPortrait
        End Class
    End Namespace
                ",
    "Artist.Painter.Rembrandt.SelfPortrait");
        }

        /*
        * Method:  AssertParse
        *
        * Parse 'source' as VB source code and get the first class name fully-qualified
        * with namespace information. That class name must match the expected class name.
        */
        private static void AssertParse(string source, string expectedClassName)
        {
            source = source.Replace("&qt", "\"");

            ExtractedClassName className = VisualBasicParserUtilities.GetFirstClassNameFullyQualified
            (
                StreamHelpers.StringToStream(source)
            );

            Assert.Equal(expectedClassName, className.Name);
        }
    }
}



