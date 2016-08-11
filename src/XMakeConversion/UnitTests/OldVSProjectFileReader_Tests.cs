// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using Microsoft.Build.Conversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    /***************************************************************************
     * 
     * Class:       OldVSProjectFileReader_Tests
     * Owner:       RGoel
     * 
     * This class contains the unit tests for the "OldVSProjectFileReader" class.  
     * See the comments in that class for a description of its purpose.
     * 
     **************************************************************************/
    [TestClass]
    public class OldVSProjectFileReader_Tests
    {
        /***********************************************************************
         *
         * Method:      OldVSProjectFileReader_Tests.CreateTemporaryProjectFile
         * Owner:       RGoel
         * 
         * Helper method which creates a temporary text file on disk with the 
         * specified contents.  Returns the temp filename as an [out] parameter.
         * 
         **********************************************************************/
        private void CreateTemporaryProjectFile
            (
            string      projectFileContents,
            out string  projectFilename
            )
        {
            projectFilename = FileUtilities.GetTemporaryFile();

            StreamWriter projectFile = new StreamWriter(projectFilename, false, Encoding.Default); // HIGHCHAR: Default means ANSI, ANSI is what VS .NET 2003 wrote. Without this, the project would be written as ASCII.
            projectFile.Write(projectFileContents);
            projectFile.Close();
        }

        /***********************************************************************
         *
         * Method:      OldVSProjectFileReader_Tests.DeleteTemporaryProjectFile
         * Owner:       RGoel
         * 
         * Helper method to delete the temporary file created via 
         * "CreateTemporaryProjectFile".
         * 
         **********************************************************************/
        private void DeleteTemporaryProjectFile
            (
            string      projectFilename
            )
        {
            File.Delete(projectFilename);
        }

        /***********************************************************************
         *
         * Method:      OldVSProjectFileReader_Tests.NoSpecialCharacters
         * Owner:       RGoel
         * 
         * Tests the OldVSProjectFileReader class, using a project file that 
         * does not contain any special characters.
         * 
         **********************************************************************/
        [TestMethod]
        public void NoSpecialCharacters 
            (
            )
        {
            // The contents of the project file that we'll be testing.  Look at the
            // right side, for a cleaner copy without all the escaping.
            string projectFileContents = 

                "<VisualStudioProject>\r\n" +                   //      <VisualStudioProject>
                "\r\n" +                                        //      
                "  <VisualBasic\r\n" +                          //        <VisualBasic
                "    ProjectType = \"Local\"\r\n" +             //          ProjectType = "Local"
                "    ProductVersion = \"7.10.3022\"\r\n" +      //          ProductVersion = "7.10.3022"
                "  >\r\n" +                                     //        >
                "  </VisualBasic>\r\n" +                        //        </VisualBasic>
                "\r\n" +                                        //      
                "</VisualStudioProject>\r\n";                   //      </VisualStudioProject>

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Create a buffer to hold 20 characters.
            char[] characterBuffer = new char[20];
            int exceptionCount = 0;
            int charactersRead = 0;

            // Read the first 20 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            Assert.AreEqual(20, charactersRead);
            Assert.AreEqual("<VisualStudioProject", new string(characterBuffer));

            // Read the next 20 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            Assert.AreEqual(20, charactersRead);
            Assert.AreEqual(">\r\n\r\n  <VisualBasic\r", new string(characterBuffer));

            // Read the next 10 characters into our buffer starting at position 5.
            charactersRead = reader.Read(characterBuffer, 5, 10);
            Assert.AreEqual(10, charactersRead);
            Assert.AreEqual(">\r\n\r\n\n    Projeasic\r", new string(characterBuffer));

            // Try reading the next 30 characters.  Since there's not enough room in our
            // buffer for 30 characters, this will fail.
            try
            {
                charactersRead = reader.Read(characterBuffer, 5, 30);
            }
            catch (ArgumentException)
            {
                exceptionCount++;
            }
            // Confirm that the proper exception was thrown and that the buffer
            // was not touched.
            Assert.AreEqual(1, exceptionCount);
            Assert.AreEqual(">\r\n\r\n\n    Projeasic\r", new string(characterBuffer));

            // Read to the end of the current line.
            string readLine = reader.ReadLine();
            Assert.AreEqual("ctType = \"Local\"", readLine);

            // Read the next line.
            readLine = reader.ReadLine();
            Assert.AreEqual("    ProductVersion = \"7.10.3022\"", readLine);

            // Read the next character.
            int character = reader.Read();
            Assert.AreEqual(' ', character);

            // Read the next character.
            character = reader.Read();
            Assert.AreEqual(' ', character);

            // Peek at the next character, but don't advance the read pointer.
            character = reader.Peek();
            Assert.AreEqual('>', character);

            // Read the next 20 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            // Read the next 20 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 20);

            // Read the next 20 characters into our buffer.  But actually, since 
            // we're almost at the end of the file, we expect that only 7 characters
            // will actually be read.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            Assert.AreEqual(7, charactersRead);
            Assert.AreEqual("ject>\r\nsualStudioPro", new string(characterBuffer));

            // Read the next 20 characters into our buffer.  Now, we're really
            // at the end of the file already, so it should come back with zero
            // characters read.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            Assert.AreEqual(0, charactersRead);
            Assert.AreEqual("ject>\r\nsualStudioPro", new string(characterBuffer));

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }

        
        /***********************************************************************
         *
         * Method:      OldVSProjectFileReader_Tests.XmlAttributesWithSpecialCharacters
         * Owner:       RGoel
         * 
         * Tests the OldVSProjectFileReader class, using a project file that 
         * contains special characters in some of the XML attribute values.
         * 
         **********************************************************************/
        [TestMethod]
        public void XmlAttributesWithSpecialCharacters 
            (
            )
        {
            // The contents of the project file that we'll be testing.  Look at the
            // right side, for a cleaner copy without all the escaping.
            string projectFileContents = 

                "<VisualStudioProject>\r\n" +                   //      <VisualStudioProject>
                "\r\n" +                                        //      
                "  <VisualBasic\r\n" +                          //        <VisualBasic
                "    ProjectType = \"Lo<cal\"\r\n" +            //          ProjectType = "Lo<cal"
                "    ProductVersion = \"7<.10.>3022\"\r\n" +    //          ProductVersion = "7<.10.>3022"
                "    A=\"blah>\" B=\"bloo<\"\r\n" +             //          A="blah>" B="bloo<"
                "  >\r\n" +                                     //        >
                "  </VisualBasic>\r\n" +                        //        </VisualBasic>
                "\r\n" +                                        //      
                "</VisualStudioProject>\r\n";                   //      </VisualStudioProject>

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Create a buffer to hold 30 characters.
            char[] characterBuffer = new char[30];
            int charactersRead = 0;

            // Read the first 30 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 30);
            Assert.AreEqual(30, charactersRead);
            Assert.AreEqual("<VisualStudioProject>\r\n\r\n  <Vi", new string(characterBuffer));

            // Read the next 30 characters into our buffer.
            charactersRead = reader.Read(characterBuffer, 0, 30);
            Assert.AreEqual(30, charactersRead);
            Assert.AreEqual("sualBasic\r\n    ProjectType = \"", new string(characterBuffer));

            // Read the next 20 characters into our buffer starting at position 10.
            // Confirm that the < and > characters within an attribute value got translated correctly.
            charactersRead = reader.Read(characterBuffer, 10, 20);
            Assert.AreEqual(20, charactersRead);
            Assert.AreEqual("sualBasic\rLo&lt;cal\"\r\n    Prod", new string(characterBuffer));

            // Read the next 20 characters into our buffer.  Confirm that the < and > characters within
            // an attribute value got translated correctly.
            charactersRead = reader.Read(characterBuffer, 0, 20);
            Assert.AreEqual(20, charactersRead);
            Assert.AreEqual("uctVersion = \"7&lt;.\r\n    Prod", new string(characterBuffer));

            // Read the remainder of the file.  Confirm that the < and > characters within
            // an attribute value got translated correctly.
            string restOfFile = reader.ReadToEnd();
            Assert.AreEqual("10.&gt;3022\"\r\n    A=\"blah&gt;\" B=\"bloo&lt;\"\r\n  >\r\n  </VisualBasic>\r\n\r\n</VisualStudioProject>\r\n", 
                restOfFile);

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }

        /***********************************************************************
         *
         * Method:      OldVSProjectFileReader_Tests.MultipleElementsOnSameLine
         * Owner:       RGoel
         * 
         * Tests the OldVSProjectFileReader class, using a project file that 
         * contains multiple XML elements with attributes on the same line.  
         * This will actually never happen in a real VS7/Everett project file,
         * but it's good to test it anyway.
         * 
         **********************************************************************/
        [TestMethod]
        public void MultipleElementsOnSameLine
            (
            )
        {
            // The contents of the project file that we'll be testing.  Look at the
            // right side, for a cleaner copy without all the escaping.
            string projectFileContents = 

                "<Elem1 Attrib1=\"bl>>ah\"/><Elem2 Attrib2=\"bl<<oo\"/>";  //  <Elem1 Attrib1="bl>>ah"/><Elem2 Attrib2="bl<<oo"/>

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Read the whole file into a string.  Confirm that the < and > characters within
            // an attribute value got translated correctly, but the < and > characters occurring
            // *outside* an attribute value are not touched.
            string wholeFile = reader.ReadToEnd();
            Assert.AreEqual("<Elem1 Attrib1=\"bl&gt;&gt;ah\"/><Elem2 Attrib2=\"bl&lt;&lt;oo\"/>\r\n", 
                wholeFile);

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }

        /// <summary>
        /// Sometimes VS.NET 2002/2003 wrote out the attribute value using single-quotes instead
        /// of double-quotes (particularly when the value itself contained double-quotes).  Therefore
        /// we make sure we handle this case properly.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void AttributeValueUsingSingleQuotes
            (
            )
        {
            // The contents of the project file that we'll be testing.
            string projectFileContents = 
                "<Elem1 Attrib1 = '1234<56789 is a \"true\" statement'/>";

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Read the whole file into a string.  Confirm that the < and > characters within
            // an attribute value got translated correctly, but the < and > characters occurring
            // *outside* an attribute value are not touched.
            string wholeFile = reader.ReadToEnd();
            Assert.AreEqual("<Elem1 Attrib1 = '1234&lt;56789 is a \"true\" statement'/>\r\n", 
                wholeFile);

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }


        /// <summary>
        /// Make sure that a lonely ampersand that is NOT legitimately used for escaping does get
        /// replaced with "&amp;" by our reader.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void AmpersandReplacement
            (
            )
        {
            // Single lonely ampersand.  This should get replaced.
            Assert.AreEqual("blah&amp;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&doo"));

            // Single lonely ampersand again, but this time with a semicolon at some point afterwards, just
            // to try and confuse the parser.
            Assert.AreEqual("blah&amp;doo;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&doo;doo"));

            // An ampersand used to escape a legitimate special character should NOT be replaced by our function.
            Assert.AreEqual("blah&lt;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&lt;doo"));

            // An ampersand used to escape a legitimate special character should NOT be replaced by our function.
            Assert.AreEqual("blah&#60;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#60;doo"));

            // If the code between the "&" and the ";" is not a legitimate number, then we SHOULD replace the "&".
            Assert.AreEqual("blah&amp;#AB;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#AB;doo"));

            // A valid hexadecimal number should NOT be replaced.
            Assert.AreEqual("blah&#xAB;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#xAB;doo"));

            // Check to make sure we can handle two replacements.
            Assert.AreEqual("blah&amp;doo&amp;heehee",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&doo&heehee"));

            // Check to make sure we can handle a replacement at the end of a string.
            Assert.AreEqual("blahdoo&amp;",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blahdoo&"));

            // Emptiness between the "&" and the ";" is not valid, and therefore the "&" should get replaced.
            Assert.AreEqual("blah&amp;;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&;doo"));

            // Emptiness between the "#" and the ";" is not valid, and therefore the "&" should get replaced.
            Assert.AreEqual("blah&amp;#;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#;doo"));

            // Emptiness between the "#x" and the ";" is not valid, and therefore the "&" should get replaced.
            Assert.AreEqual("blah&amp;#x;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#x;doo"));

            // Even characters beyond 256 should be considered valid.
            Assert.AreEqual("blah&#280;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&#280;doo"));

            // The VS.NET 2003 XML writer even persisted things like the copyright symbol as an entity.
            // So we need to be careful about not touching those.
            Assert.AreEqual("blah&copy;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&copy;doo"));

            // The VS.NET 2003 XML writer even persisted things like the copyright symbol as an entity.
            // So we need to be careful about not touching those.
            Assert.AreEqual("blah&amp;COPY;doo",
                OldVSProjectFileReader.ReplaceSpecialCharactersInXmlAttributeString("blah&COPY;doo"));
        }


        /// <summary>
        /// VS .NET 2002/2003 wrote out projects in invalid XML in some cases.  For example,
        /// an "&" character is not allowed in an XML attribute value, unless it is being
        /// used to escape some other special character (where that other character may itself
        /// be an ampersand).  However, the VS .NET 2002/2003 XML writer did this all the time.
        /// In order for System.Xml to be able to handle it, we need to translate those bogus
        /// "&"'s to "&amp;" before letting System.Xml have at it.
        /// </summary>
        /// <owner>RGoel</owner>
        [TestMethod]
        public void Regress322573
            (
            )
        {
            // The contents of the project file that we'll be testing.
            string projectFileContents = 
                "<Elem1 StartArguments = \"???action=16&requestid=1000036&#14CA053601F66928BF0550E395A714E72C8D6066???  /HeadTraxStartversion 5.6.0.66 /RunningFromHeadTraxStart yes /HTXMutexName HTXMutex344\"/>";

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Read the whole file into a string.  Confirm that the & character within
            // an attribute value got translated correctly.
            string wholeFile = reader.ReadToEnd();
            Assert.AreEqual("<Elem1 StartArguments = \"???action=16&amp;requestid=1000036&amp;#14CA053601F66928BF0550E395A714E72C8D6066???  /HeadTraxStartversion 5.6.0.66 /RunningFromHeadTraxStart yes /HTXMutexName HTXMutex344\"/>\r\n", 
                wholeFile);

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }


        /// <summary>
        /// VS .NET wrote out projects in ANSI format by default.
        /// We want to make sure those characters don't get stripped by our reader.
        /// </summary>
        [TestMethod]
        public void Regress184573()
        {
            // The contents of the project file that we'll be testing.  Look at the
            // right side, for a cleaner copy without all the escaping.
            char c = '�';
            string projectFileContents = new string(c,1);

            // Create a temp file on disk with the above contents.
            string projectFilename;
            this.CreateTemporaryProjectFile(projectFileContents, out projectFilename);

            // Instantiate our class with the project file.
            OldVSProjectFileReader reader = new OldVSProjectFileReader(projectFilename);

            // Read the whole file into a string.  
            string wholeFile = reader.ReadToEnd();
            Assert.IsTrue(wholeFile.Length > 0, "High-bit character was stripped.");

           // Create two different encodings.
            Encoding defaultEncoding = Encoding.Default;
            Encoding unicode = Encoding.Unicode;

            // Convert the string into a byte array.
            byte[] unicodeBytes = unicode.GetBytes(projectFileContents);
            // Perform the conversion from one encoding to the other.
            byte[] defaultEncodingBytes = Encoding.Convert(unicode, defaultEncoding, unicodeBytes);

            // Convert the new byte[] into a char[] and then into a string.
            char[] defaultEncodingChars = new char[defaultEncoding.GetCharCount(defaultEncodingBytes, 0, defaultEncodingBytes.Length)];
            defaultEncoding.GetChars(defaultEncodingBytes, 0, defaultEncodingBytes.Length, defaultEncodingChars, 0);

            Assert.IsTrue(defaultEncodingChars.Length > 0);
            Assert.IsTrue(wholeFile[0] == defaultEncodingChars[0], String.Format("Expected ANSI encoding of '{0}' to result in '{0}'. Instead it was '{1}'", c, defaultEncodingChars[0], wholeFile[0])
            );

            // Clean up.
            reader.Close();
            this.DeleteTemporaryProjectFile(projectFilename);
        }

        /// <summary>
        /// Tests that a single ampersand replacement works correctly at the beginning of 
        /// a string, middle of a string, and end of a string.
        /// </summary>
        [TestMethod]
        public void ReplaceSingleAmpersand()
        {
            Assert.AreEqual("&amp;1234", OldVSProjectFileReader.ReplaceAmpersandWithLiteral("&1234", 0));
            Assert.AreEqual("12&amp;34", OldVSProjectFileReader.ReplaceAmpersandWithLiteral("12&34", 2));
            Assert.AreEqual("1234&amp;", OldVSProjectFileReader.ReplaceAmpersandWithLiteral("1234&", 4));
        }
    }
}
