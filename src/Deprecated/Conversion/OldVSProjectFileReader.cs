// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Globalization;

using error = Microsoft.Build.Shared.ErrorUtilities;

namespace Microsoft.Build.Conversion
{
    /// <summary>
    /// This class implements a custom text reader for the old VS7/Everett 
    /// project file format.  The old format allowed certain XML special 
    /// characters to be present within an XML attribute value.  For example,
    ///
    ///     &lt;MyElement MyAttribute="My --> Value" /&gt;
    ///
    /// However, the System.Xml classes are more strict, and do not allow
    /// the &lt; or &gt; characters to exist within an attribute value.  But 
    /// the conversion utility still needs to be able to convert all old
    /// project files.  So the OldVSProjectFileReader class implements
    /// the TextReader interface, thereby effectively intercepting all of
    /// the calls which are used by the XmlTextReader to actually read the
    /// raw text out of the file.  As we are reading the text out of the 
    /// file, we replace all &gt; (less-than) characters inside attribute values with "&gt;",
    /// etc.  The XmlTextReader has no idea that this is going on, but 
    /// no longer complains about invalid characters.
    /// </summary>
    /// <owner>rgoel</owner>
    internal sealed class OldVSProjectFileReader : TextReader
    {
        // This is the underlying text file where we will be reading the raw text
        // from.
        private StreamReader    oldVSProjectFile;
        
        // We will be reading one line at a time out of the text file, and caching
        // it here.
        private StringBuilder   singleLine;
        
        // The "TextReader" interface that we are implementing only allows
        // forward access through the file.  You cannot seek to a random location
        // or read backwards.  This variable is the index into the "singleLine"
        // string above, which indicates how far the caller has read.  Once we
        // reach the end of "singleLine", we'll go read a new line from the file.
        private int             currentReadPositionWithinSingleLine;

        /// <summary>
        /// Constructor, initialized using the filename of an existing old format
        /// project file on disk.
        /// </summary>
        /// <owner>rgoel</owner>
        /// <param name="filename"></param>
        internal OldVSProjectFileReader
            (
            string filename
            )
        {
            this.oldVSProjectFile = new StreamReader(filename, Encoding.Default); // HIGHCHAR: Default means ANSI, ANSI is what VS .NET 2003 wrote. Without this, the project would be read as ASCII.
            
            this.singleLine = new StringBuilder();
            this.currentReadPositionWithinSingleLine = 0;
        }

        /// <summary>
        /// Releases all locks and closes all handles on the underlying file.
        /// </summary>
        /// <owner>rgoel</owner>
        public override void Close
            (
            )
        {
            oldVSProjectFile.Close();
        }

        /// <summary>
        /// Returns the next character in the file, without actually advancing
        /// the read pointer.  Returns -1 if we're already at the end of the file.  
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override int Peek
            (
            )
        {
            // If necessary, read a new line of text into our internal buffer 
            // (this.singleLine).
            if (!this.ReadLineIntoInternalBuffer())
            {
                // If we've reached the end of the file, return -1.
                return -1;
            }

            // Return the next character, but don't advance the current position.
            return this.singleLine[this.currentReadPositionWithinSingleLine];
        }

        /// <summary>
        /// Returns the next character in the file, and advances the read pointer.  
        /// Returns -1 if we're already at the end of the file.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override int Read
            (
            )
        {
            // Use our "Peek" functionality above.
            int returnCharacter = this.Peek();

            // If there's a character there, advance the read pointer by one.
            if (returnCharacter != -1)
            {
                this.currentReadPositionWithinSingleLine++;
            }

            return returnCharacter;
        }

        /// <summary>
        /// Reads the specified number of characters into the caller's buffer, 
        /// starting at the specified index into the caller's buffer.  Returns
        /// the number of characters read, or 0 if we're already at the end of
        /// the file.
        /// </summary>
        /// <param name="bufferToReadInto"></param>
        /// <param name="startIndexIntoBuffer"></param>
        /// <param name="charactersToRead"></param>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override int Read
            (
            char[]  bufferToReadInto,       // The buffer to read the data into.
            int     startIndexIntoBuffer,   // The index into "bufferToReadInto"
            int     charactersToRead        // The number of characters to read.
            )
        {
            // Make sure there's enough room in the caller's buffer for what he's
            // asking us to do.
            if ((startIndexIntoBuffer + charactersToRead) > bufferToReadInto.Length)
            {
                // End-user should never see this message, so it doesn't need to be localized.
                throw new ArgumentException("Cannot write past end of user's buffer.", nameof(charactersToRead));
            }

            int charactersCopied = 0;

            // Keep looping until we've read in the number of characters requested.
            // If we reach the end of file, we'll break out early.
            while (0 < charactersToRead)
            {
                // Read more data from the underlying file if necessary.
                if (!this.ReadLineIntoInternalBuffer())
                {
                    // If we've reached the end of the underlying file, exit the 
                    // loop.
                    break;
                }

                // We're going to copy characters from our cached singleLine to the caller's
                // buffer.  The number of characters to copy is the lesser of (the remaining
                // characters in our cached singleLine) and (the number of characters remaining
                // before we've fulfilled the caller's request).
                int charactersToCopy = (this.singleLine.Length - currentReadPositionWithinSingleLine);
                if (charactersToCopy > charactersToRead)
                {
                    charactersToCopy = charactersToRead;
                }

                // Copy characters from our cached "singleLine" to the caller's buffer.
                this.singleLine.ToString().CopyTo(this.currentReadPositionWithinSingleLine, bufferToReadInto, 
                    startIndexIntoBuffer, charactersToCopy);

                // Update all counts and indices.
                startIndexIntoBuffer += charactersToCopy;
                this.currentReadPositionWithinSingleLine += charactersToCopy;
                charactersCopied += charactersToCopy;
                charactersToRead -= charactersToCopy;
            }

            return charactersCopied;
        }

        /// <summary>
        /// Not implemented.  Our class only supports reading from a file, which
        /// can't change beneath the covers while we're reading from it.  Therefore,
        /// a blocking read doesn't make sense for our scenario.  (A blocking read
        /// is where you wait until the requested number of characters actually
        /// become available ... which is never going to happen if you've already
        /// reached the end of a file.)
        /// </summary>
        /// <param name="bufferToReadInto"></param>
        /// <param name="startIndexIntoBuffer"></param>
        /// <param name="charactersToRead"></param>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override int ReadBlock
            (
            char[]  bufferToReadInto, 
            int     startIndexIntoBuffer, 
            int     charactersToRead
            )
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads a single line of text, and returns it as a string, not including the
        /// terminating line-ending characters.  If we were at the end of the file,
        /// return null.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override string ReadLine
            (
            )
        {
            // Read a new line from the underlying file if necessary (that is, only
            // if our currently cached singleLine has already been used up).
            if (!this.ReadLineIntoInternalBuffer())
            {
                // If we reached the end of the underlying file, return null.
                return null;
            }

            // We now have a single line of text cached in our "singleLine" variable.
            // Just return that, or the portion of that which hasn't been already read
            // by the caller).
            string result = this.singleLine.ToString(this.currentReadPositionWithinSingleLine,
                this.singleLine.Length - this.currentReadPositionWithinSingleLine);

            // The caller has read the entirety of our cached "singleLine", so update
            // our read pointer accordingly.
            this.currentReadPositionWithinSingleLine = this.singleLine.Length;

            // Strip off the line endings before returning to caller.
            char[] lineEndingCharacters = new char[] {'\r', '\n'};
            return result.Trim(lineEndingCharacters);
        }

        /// <summary>
        /// Reads the remainder of the file, and returns it as a string.  Returns
        /// an empty string if we've already reached the end of the file.
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        public override string ReadToEnd
            (
            )
        {
            // This is what we're going to return to the caller.
            StringBuilder result = new StringBuilder();

            // Keep reading lines of text out of the underlying file, one line at
            // a time.
            while (true)
            {
                if (!this.ReadLineIntoInternalBuffer())
                {
                    // Exit the loop when we've reached the end of the underlying
                    // file.
                    break;
                }

                // Append the line of text to the resulting output.
                result.Append(this.singleLine.ToString(this.currentReadPositionWithinSingleLine, 
                    this.singleLine.Length - this.currentReadPositionWithinSingleLine));

                this.currentReadPositionWithinSingleLine = this.singleLine.Length;
            }

            return result.ToString();
        }

        /// <summary>
        /// And this is where the real magic happens.  If our currently cached
        /// "singleLine" has been used up, we read a new line of text from the 
        /// underlying text file.  But as we read the line of text from the file,
        /// we immediately replace all instances of special characters that occur
        /// within double-quotes with the corresponding XML-friendly equivalents.
        /// For example, if the underlying text file contained this:
        ///
        ///     &lt;MyElement MyAttribute="My --&gt; Value" /&gt;
        ///
        /// then we would read it in and immediately convert it to this:
        ///
        ///     &lt;MyElement MyAttribute="My --&gt; Value" /&gt;
        ///
        /// and we would store it this way in our "singleLine", so that the callers
        /// never know the difference.
        /// 
        /// This method returns true on success, and false if we were unable to
        /// read a new line (due to end of file).
        /// </summary>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        private bool ReadLineIntoInternalBuffer
            (
            )
        {
            // Only do the work if we've already used up the data in the currently
            // cached "singleLine".
            if (this.currentReadPositionWithinSingleLine >= this.singleLine.Length)
            {
                // Read a line of text from the underlying file.
                string lineFromProjectFile = this.oldVSProjectFile.ReadLine();
                if (lineFromProjectFile == null)
                {
                    // If we've reached the end of the file, return false.
                    return false;
                }

                // Take the line of text just read, and replace all special characters
                // with the escaped XML-friendly string equivalents.
                this.singleLine = new StringBuilder(this.ReplaceSpecialCharacters(lineFromProjectFile));
                
                // The underlying StreamReader.ReadLine method doesn't give us the 
                // trailing line endings, so add them back ourselves.
                this.singleLine.Append(Environment.NewLine);
                
                // So now we have a new cached "singleLine".  Reset the read pointer
                // to the beginning of the new line just read.
                this.currentReadPositionWithinSingleLine = 0;
            }

            return true;
        }

        /// <summary>
        /// This method uses a regular expression to search for the stuff in
        /// between double-quotes.  We obviously don't want to touch the stuff
        /// OUTSIDE of double-quotes, because then we would be mucking with the 
        /// real angle-brackets that delimit the XML element names, etc.
        /// </summary>
        /// <param name="originalLine"></param>
        /// <returns></returns>
        /// <owner>rgoel</owner>
        private string ReplaceSpecialCharacters
            (
            string originalLine
            )
        {
            // Find the stuff within double-quotes, and send it off to the 
            // "ReplaceSpecialCharactersInXmlAttribute" for proper replacement of
            // the special characters.
            Regex attributeValueInsideDoubleQuotesPattern = new Regex("= *\"[^\"]*\"");

            string replacedStuffInsideDoubleQuotes = attributeValueInsideDoubleQuotesPattern.Replace(originalLine, 
                new MatchEvaluator(this.ReplaceSpecialCharactersInXmlAttribute));
                
            // Find the stuff within single-quotes, and send it off to the 
            // "ReplaceSpecialCharactersInXmlAttribute" for proper replacement of
            // the special characters.
            Regex attributeValueInsideSingleQuotesPattern = new Regex("= *'[^']*'");

            string replacedStuffInsideSingleQuotes = attributeValueInsideSingleQuotesPattern.Replace(replacedStuffInsideDoubleQuotes, 
                new MatchEvaluator(this.ReplaceSpecialCharactersInXmlAttribute));
                
            return replacedStuffInsideSingleQuotes;
        }

        /// <summary>
        /// This method is used as the delegate that is passed into Regex.Replace.
        /// It a regular expression to search for the stuff in
        /// between double-quotes.  We obviously don't want to touch the stuff
        /// OUTSIDE of double-quotes, because then we would be mucking with the 
        /// real angle-brackets that delimit the XML element names, etc.
        /// </summary>
        /// <param name="xmlAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private string ReplaceSpecialCharactersInXmlAttribute
            (
            Match xmlAttribute
            )
        {
            // We've been given the string for the attribute value (i.e., all the stuff
            // within double-quotes, including the double-quotes).  Replace all the special characters
            // within it, and return the new string.
            return ReplaceSpecialCharactersInXmlAttributeString(xmlAttribute.Value);
        }

        /// <summary>
        /// This method actually does the replacement of special characters within the 
        /// text of the XML attribute.
        /// </summary>
        /// <param name="xmlAttributeText">Input string</param>
        /// <returns>New string with all the replacements (e.g. "&amp;" becomes "&amp;amp;", etc.)</returns>
        /// <owner>RGoel</owner>
        internal static string ReplaceSpecialCharactersInXmlAttributeString
            (
            string xmlAttributeText
            )
        {
            // Replace the special characters with their XML-friendly escaped equivalents.  The
            // "<" and ">" signs are easy, because if they exist at all within the value of an
            // XML attribute, we know that they need to be replaced with "&lt;" and "&gt;" 
            // respectively.
            xmlAttributeText = xmlAttributeText.Replace("<", "&lt;");
            xmlAttributeText = xmlAttributeText.Replace(">", "&gt;");
            xmlAttributeText = ReplaceNonEscapingAmpersands(xmlAttributeText);
            
            return xmlAttributeText;
        }

        // Note -- the comment below is rendered a little confusing by escaping for XML doc compiler. Read "&amp;" as "&"
        // and "&amp;amp;" as "&amp;". Or just look at the intellisense tooltip.
        /// <summary>
        /// This method scans the strings for "&amp;" characters, and based on what follows
        /// the "&amp;" character, it determines whether the "&amp;" character needs to be replaced
        /// with "&amp;amp;".  The old XML parser used in the VS.NET 2002/2003 project system
        /// was quite inconsistent in its treatment of escaped characters in XML, so here
        /// we're having to make up for those bugs.  The new XML parser (System.Xml) 
        /// is much more strict in enforcing proper XML syntax, and therefore doesn't 
        /// tolerate "&amp;" characters in the XML attribute value, unless the "&amp;" is being
        /// used to escape some special character.
        /// </summary>
        /// <param name="xmlAttributeText">Input string</param>
        /// <returns>New string with all the replacements (e.g. "&amp;" becomes "&amp;amp;", etc.)</returns>
        /// <owner>RGoel</owner>
        private static string ReplaceNonEscapingAmpersands
            (
            string xmlAttributeText
            )
        {
            // Ampersands are a little trickier, because some instances of "&" we need to leave
            // untouched, and some we need to replace with "&amp;".  For example, 
            //      aaa&bbb         should be replaced with         aaa&amp;bbb
            // But:
            //      aaa&lt;bbb      should not be touched.
            
            // Loop through each instance of "&"
            int indexOfAmpersand = xmlAttributeText.IndexOf('&');
            while (indexOfAmpersand != -1)
            {
                // If an "&" was found, search for the next ";" following the "&".
                int indexOfNextSemicolon = xmlAttributeText.IndexOf(';', indexOfAmpersand);
                if (indexOfNextSemicolon == -1)
                {
                    // No semicolon means that the ampersand was really intended to be a literal
                    // ampersand and therefore we need to replace it with "&amp;".  For example,
                    //
                    //     aaa&bbb          should get replaced with        aaa&amp;bbb
                    xmlAttributeText = ReplaceAmpersandWithLiteral(xmlAttributeText, indexOfAmpersand);
                }
                else
                {
                    // We found the semicolon.  Capture the characters between (but not
                    // including) the "&" and ";".
                    string entityName = xmlAttributeText.Substring(indexOfAmpersand + 1, 
                        indexOfNextSemicolon - indexOfAmpersand - 1);

                    // Perf note: Here we are walking through the entire list of entities, and
                    // doing a string comparison for each.  This is expensive, but this code
                    // should only get executed in fairly rare circumstances.  It's not very 
                    // common for people to have these embedded into their project files.
                    bool foundEntity = false;
                    for (int i = 0 ; i < entities.Length ; i++)
                    {
                        // Case-sensitive comparison to see if the entity name matches any of
                        // the well-known ones that were emitted by the XML writer in the VS.NET
                        // 2002/2003 project system.
                        if (String.Equals(entityName, entities[i], StringComparison.Ordinal))
                        {
                            foundEntity = true;
                            break;
                        }
                    }

                    // If it didn't match a well-known entity name, then the next thing to 
                    // check is if it represents an ASCII code.  For example, in an XML
                    // attribute, if I wanted to represent the "+" sign, I could do this:
                    //
                    //          &#43;
                    //
                    if (!foundEntity && (entityName.Length > 0) && (entityName[0] == '#'))
                    {
                        // At this point, we know entityName is something like "#1234" or "#x1234abcd"
                        bool isNumber = false;
                        
                        // A lower-case "x" in the second position indicates a hexadecimal value.
                        if ((entityName.Length > 2) && (entityName[1] == 'x'))
                        {
                            isNumber = true;

                            // It's a hexadecimal number.  Make sure every character of the entity
                            // is in fact a valid hexadecimal character.
                            for (int i = 2; i < entityName.Length; i++)
                            {
                                if (!Uri.IsHexDigit(entityName[i]))
                                {
                                    isNumber = false;
                                    break;
                                }
                            }
                        }
                        else if (entityName.Length > 1)
                        {
                            // Otherwise it's a decimal value.
                            isNumber = true;

                            // ake sure every character of the entity is in fact a valid decimal number.
                            for (int i = 1; i < entityName.Length; i++)
                            {
                                if (!Char.IsNumber(entityName[i]))
                                {
                                    isNumber = false;
                                    break;
                                }
                            }
                        }

                        if (isNumber)
                        {
                            foundEntity = true;
                        }
                    }

                    // If the ampersand did not precede an actual well-known entity, then we DO want to 
                    // replace the "&" with a "&amp;".  Otherwise we don't.
                    if (!foundEntity)
                    {
                        xmlAttributeText = ReplaceAmpersandWithLiteral(xmlAttributeText, indexOfAmpersand);
                    }
                }

                // We're done process that particular "&".  Now find the next one.
                indexOfAmpersand = xmlAttributeText.IndexOf('&', indexOfAmpersand + 1);
            }

            return xmlAttributeText;
        }

        // Note -- the comment below is rendered a little confusing by escaping for XML doc compiler. Read "&amp;" as "&"
        // and "&amp;amp;" as "&amp;". Or just look at the intellisense tooltip.
        /// <summary>
        /// Replaces a single instance of an "&amp;" character in a string with "&amp;amp;" and returns the new string.
        /// </summary>
        /// <param name="originalString">Original string where we should find an "&amp;" character.</param>
        /// <param name="indexOfAmpersand">The index of the "&amp;" which we want to replace.</param>
        /// <returns>The new string with the "&amp;" replaced with "&amp;amp;".</returns>
        /// <owner>RGoel</owner>
        internal static string ReplaceAmpersandWithLiteral
            (
            string originalString,
            int indexOfAmpersand
            )
        {
            error.VerifyThrow(originalString[indexOfAmpersand] == '&',
                "Caller passed in a string that doesn't have an '&' character in the specified location.");

            StringBuilder replacedString = new StringBuilder();

            replacedString.Append(originalString, 0, indexOfAmpersand);
            replacedString.Append("&amp;");
            replacedString.Append(originalString, indexOfAmpersand + 1, originalString.Length - indexOfAmpersand + 1);

            return replacedString.ToString();
        }

        // This is the complete list of well-known entity names that were written out
        // by the XML writer in the VS.NET 2002/2003 project system.  This list was
        // taken directly from the source code.
        private static readonly string[] entities = 
        {
            "quot",          // 
            "amp",           // & - ampersand
            "apos",          // ' - apostrophe //// not part of HTML!
            "lt",            // < less than
            "gt",            // > greater than
            "nbsp",         // Non breaking space
            "iexcl",        //
            "cent",         // cent
            "pound",        // pound
            "curren",       // currency
            "yen",          // yen
            "brvbar",       // vertical bar
            "sect",         // section
            "uml",          //
            "copy",         // Copyright
            "ordf",         //
            "laquo",        //
            "not",          //
            "shy",          //
            "reg",          // Registered TradeMark
            "macr",         //
            "deg",          //
            "plusmn",       //
            "sup2",         //
            "sup3",         //
            "acute",        //
            "micro",        //
            "para",         //
            "middot",       //
            "cedil",        //
            "sup1",         //
            "ordm",         //
            "raquo",        //
            "frac14",       // 1/4
            "frac12",       // 1/2
            "frac34",       // 3/4
            "iquest",       // Inverse question mark
            "Agrave",       // Capital A grave accent
            "Aacute",       // Capital A acute accent
            "Acirc",        // Capital A circumflex accent
            "Atilde",       // Capital A tilde
            "Auml",         // Capital A dieresis or umlaut mark
            "Aring",        // Capital A ring
            "AElig",        // Capital AE dipthong (ligature)
            "Ccedil",       // Capital C cedilla
            "Egrave",       // Capital E grave accent
            "Eacute",       // Capital E acute accent
            "Ecirc",        // Capital E circumflex accent
            "Euml",         // Capital E dieresis or umlaut mark
            "Igrave",       // Capital I grave accent
            "Iacute",       // Capital I acute accent
            "Icirc",        // Capital I circumflex accent
            "Iuml",         // Capital I dieresis or umlaut mark
            "ETH",          // Capital Eth Icelandic
            "Ntilde",       // Capital N tilde
            "Ograve",       // Capital O grave accent
            "Oacute",       // Capital O acute accent
            "Ocirc",        // Capital O circumflex accent
            "Otilde",       // Capital O tilde
            "Ouml",         // Capital O dieresis or umlaut mark
            "times",        // multiply or times
            "Oslash",       // Capital O slash
            "Ugrave",       // Capital U grave accent
            "Uacute",       // Capital U acute accent
            "Ucirc",        // Capital U circumflex accent
            "Uuml",         // Capital U dieresis or umlaut mark;
            "Yacute",       // Capital Y acute accent
            "THORN",        // Capital THORN Icelandic
            "szlig",        // Small sharp s German (sz ligature)
            "agrave",       // Small a grave accent
            "aacute",       // Small a acute accent
            "acirc",        // Small a circumflex accent
            "atilde",       // Small a tilde
            "auml",         // Small a dieresis or umlaut mark
            "aring",        // Small a ring
            "aelig",        // Small ae dipthong (ligature)
            "ccedil",       // Small c cedilla
            "egrave",       // Small e grave accent
            "eacute",       // Small e acute accent
            "ecirc",        // Small e circumflex accent
            "euml",         // Small e dieresis or umlaut mark
            "igrave",       // Small i grave accent
            "iacute",       // Small i acute accent
            "icirc",        // Small i circumflex accent
            "iuml",         // Small i dieresis or umlaut mark
            "eth",          // Small eth Icelandic
            "ntilde",       // Small n tilde
            "ograve",       // Small o grave accent
            "oacute",       // Small o acute accent
            "ocirc",        // Small o circumflex accent
            "otilde",       // Small o tilde
            "ouml",         // Small o dieresis or umlaut mark
            "divide",       // divide
            "oslash",       // Small o slash
            "ugrave",       // Small u grave accent
            "uacute",       // Small u acute accent
            "ucirc",        // Small u circumflex accent
            "uuml",         // Small u dieresis or umlaut mark
            "yacute",       // Small y acute accent
            "thorn",        // Small thorn Icelandic
            "yuml",         // Small y dieresis or umlaut mark
            "OElig",        // latin capital ligature oe, U0152 ISOlat2
            "oelig",        // latin small ligature oe, U0153 ISOlat2
            "Scaron",       // latin capital letter s with caron, U0160 ISOlat2
            "scaron",       // latin small letter s with caron, U0161 ISOlat2
            "Yuml",         // latin capital letter y with diaeresis, U0178 ISOlat2
            "fnof",         // latin small f with hook, =function, =florin, U0192 ISOtech
            "circ",         // modifier letter circumflex accent, U02C6 ISOpub
            "tilde",        // small tilde, U02DC ISOdia
            "Alpha",        // greek capital letter alpha
            "Beta",         // greek capital letter beta
            "Gamma",        // greek capital letter gamma
            "Delta",        // greek capital letter delta
            "Epsilon",      // greek capital letter epsilon
            "Zeta",         // greek capital letter zeta
            "Eta",          // greek capital letter eta
            "Theta",        // greek capital letter theta
            "Iota",         // greek capital letter iota 
            "Kappa",        // greek capital letter kappa
            "Lambda",       // greek capital letter lambda
            "Mu",           // greek capital letter mu
            "Nu",           // greek capital letter nu
            "Xi",           // greek capital letter xi
            "Omicron",      // greek capital letter omicron
            "Pi",           // greek capital letter pi
            "Rho",          // greek capital letter rho
            "Sigma",        // greek capital letter sigma
            "Tau",          // greek capital letter tau
            "Upsilon",      // greek capital letter upsilon
            "Phi",          // greek capital letter phi
            "Chi",          // greek capital letter chi
            "Psi",          // greek capital letter psi   
            "Omega",        // greek capital letter omega
            "alpha",        // greek small letter alpha
            "beta",         // greek small letter beta
            "gamma",        // greek small letter gamma
            "delta",        // greek small letter delta
            "epsilon",      // greek small letter epsilon
            "zeta",         // greek small letter zeta
            "eta",          // greek small letter eta
            "theta",        // greek small letter theta
            "iota",         // greek small letter iota 
            "kappa",        // greek small letter kappa
            "lambda",       // greek small letter lambda
            "mu",           // greek small letter mu
            "nu",           // greek small letter nu
            "xi",           // greek small letter xi
            "omicron",      // greek small letter omicron
            "pi",           // greek small letter pi
            "rho",          // greek small letter rho
            "sigmaf",       // greek small final sigma
            "sigma",        // greek small letter sigma
            "tau",          // greek small letter tau
            "upsilon",      // greek small letter upsilon
            "phi",          // greek small letter phi
            "chi",          // greek small letter chi
            "psi",          // greek small letter psi   
            "omega",        // greek small letter omega
            "thetasym",     // greek small letter theta symbol, U03D1 NEW
            "upsih",        // greek upsilon with hook symbol
            "piv",          // greek pi symbol
            "ensp",        // en space, U2002 ISOpub
            "emsp",        // em space, U2003 ISOpub
            "thinsp",      // thin space, U2009 ISOpub
            "zwnj",        // zero width non-joiner, U200C NEW RFC 2070
            "zwj",         // zero width joiner, U200D NEW RFC 2070
            "lrm",         // left-to-right mark, U200E NEW RFC 2070
            "rlm",         // right-to-left mark, U200F NEW RFC 2070
            "ndash",       // en dash, U2013 ISOpub
            "mdash",       // em dash, U2014 ISOpub
            "lsquo",       // left single quotation mark, U2018 ISOnum
            "rsquo",       // right single quotation mark, U2019 ISOnum
            "sbquo",       // single low-9 quotation mark, U201A NEW
            "ldquo",       // left double quotation mark, U201C ISOnum
            "rdquo",       // right double quotation mark, U201D ISOnum
            "bdquo",       // double low-9 quotation mark, U201E NEW
            "dagger",      // dagger, U2020 ISOpub
            "Dagger",      // double dagger, U2021 ISOpub
            "bull",        // bullet, =black small circle, U2022 ISOpub
            "hellip",      // horizontal ellipsis, =three dot leader, U2026 ISOpub
            "permil",      // per mille sign, U2030 ISOtech
            "prime",       // prime, =minutes, =feet, U2032 ISOtech
            "Prime",       // double prime, =seconds, =inches, U2033 ISOtech
            "lsaquo",      // single left-pointing angle quotation mark, U2039 ISO proposed
            "rsaquo",      // single right-pointing angle quotation mark, U203A ISO proposed
            "oline",       // overline, spacing overscore
            "frasl",       // fraction slash
            "image",       // blackletter capital I, =imaginary part, U2111 ISOamso 
            "weierp",      // script capital P, =power set, =Weierstrass p, U2118 ISOamso 
            "real",        // blackletter capital R, =real part symbol, U211C ISOamso 
            "trade",       // trade mark sign, U2122 ISOnum 
            "alefsym",     // alef symbol, =first transfinite cardinal, U2135 NEW 
            "larr",        // leftwards arrow, U2190 ISOnum 
            "uarr",        // upwards arrow, U2191 ISOnum
            "rarr",        // rightwards arrow, U2192 ISOnum 
            "darr",        // downwards arrow, U2193 ISOnum 
            "harr",        // left right arrow, U2194 ISOamsa 
            "crarr",       // downwards arrow with corner leftwards, =carriage return, U21B5 NEW 
            "lArr",        // leftwards double arrow, U21D0 ISOtech 
            "uArr",        // upwards double arrow, U21D1 ISOamsa 
            "rArr",        // rightwards double arrow, U21D2 ISOtech 
            "dArr",        // downwards double arrow, U21D3 ISOamsa 
            "hArr",        // left right double arrow, U21D4 ISOamsa 
            "forall",      // for all, U2200 ISOtech 
            "part",        // partial differential, U2202 ISOtech  
            "exist",       // there exists, U2203 ISOtech 
            "empty",       // empty set, =null set, =diameter, U2205 ISOamso 
            "nabla",       // nabla, =backward difference, U2207 ISOtech 
            "isin",        // element of, U2208 ISOtech 
            "notin",       // not an element of, U2209 ISOtech 
            "ni",          // contains as member, U220B ISOtech 
            "prod",        // n-ary product, =product sign, U220F ISOamsb 
            "sum",         // n-ary sumation, U2211 ISOamsb 
            "minus",       // minus sign, U2212 ISOtech 
            "lowast",      // asterisk operator, U2217 ISOtech 
            "radic",       // square root, =radical sign, U221A ISOtech 
            "prop",        // proportional to, U221D ISOtech 
            "infin",       // infinity, U221E ISOtech 
            "ang",         // angle, U2220 ISOamso 
            "and",         // logical and, =wedge, U2227 ISOtech 
            "or",          // logical or, =vee, U2228 ISOtech 
            "cap",         // intersection, =cap, U2229 ISOtech 
            "cup",         // union, =cup, U222A ISOtech 
            "int",         // integral, U222B ISOtech 
            "there4",      // therefore, U2234 ISOtech 
            "sim",         // tilde operator, =varies with, =similar to, U223C ISOtech 
            "cong",        // approximately equal to, U2245 ISOtech 
            "asymp",       // almost equal to, =asymptotic to, U2248 ISOamsr 
            "ne",          // not equal to, U2260 ISOtech 
            "equiv",       // identical to, U2261 ISOtech 
            "le",          // less-than or equal to, U2264 ISOtech 
            "ge",          // greater-than or equal to, U2265 ISOtech 
            "sub",         // subset of, U2282 ISOtech 
            "sup",         // superset of, U2283 ISOtech 
            "nsub",        // not a subset of, U2284 ISOamsn 
            "sube",        // subset of or equal to, U2286 ISOtech 
            "supe",        // superset of or equal to, U2287 ISOtech 
            "oplus",       // circled plus, =direct sum, U2295 ISOamsb 
            "otimes",      // circled times, =vector product, U2297 ISOamsb 
            "perp",        // up tack, =orthogonal to, =perpendicular, U22A5 ISOtech 
            "sdot",        // dot operator, U22C5 ISOamsb 
            "lceil",       // left ceiling, =apl upstile, U2308, ISOamsc  
            "rceil",       // right ceiling, U2309, ISOamsc  
            "lfloor",      // left floor, =apl downstile, U230A, ISOamsc  
            "rfloor",      // right floor, U230B, ISOamsc  
            "lang",        // left-pointing angle bracket, =bra, U2329 ISOtech 
            "rang",        // right-pointing angle bracket, =ket, U232A ISOtech 
            "loz",         // lozenge, U25CA ISOpub 
            "spades",      // black spade suit, U2660 ISOpub 
            "clubs",       // black club suit, =shamrock, U2663 ISOpub 
            "hearts",      // black heart suit, =valentine, U2665 ISOpub 
            "diams"        // black diamond suit, U2666 ISOpub 
        };
    }
}
