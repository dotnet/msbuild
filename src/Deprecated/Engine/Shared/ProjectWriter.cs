// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class is used to save MSBuild project files. It contains special handling for MSBuild notations that are not saved
    /// correctly by the XML DOM's default save mechanism.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class ProjectWriter : XmlTextWriter
    {
        #region Regular expressions for item vector transforms

        /**************************************************************************************************************************
         * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ItemExpander class -- if the
         * description of an item vector changes, the expressions must be updated in both places.
         *************************************************************************************************************************/

        // the portion of the expression that matches the item type or metadata name, eg: "foo123"
        // Note that the pattern is more strict than the rules for valid XML element names.
        internal const string itemTypeOrMetadataNameSpecification = @"[A-Za-z_][A-Za-z_0-9\-]*";

        // description of an item vector transform, including the optional separator specification
        private const string itemVectorTransformSpecification =
            @"(?<PREFIX>@\(\s*)
                (?<TYPE>" + itemTypeOrMetadataNameSpecification + @")
                (?<TRANSFORM_SPECIFICATION>(?<ARROW>\s*->\s*)(?<TRANSFORM>'[^']*'))
                (?<SEPARATOR_SPECIFICATION>\s*,\s*'[^']*')?
              (?<SUFFIX>\s*\))";
                                                      // )

        // regular expression used to match item vector transforms
        // internal for unit testing only
        internal static readonly Regex itemVectorTransformPattern = new Regex(itemVectorTransformSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // description of an item vector transform, including the optional separator specification, but with no (named) capturing
        // groups -- see the WriteString() method for details
        private const string itemVectorTransformRawSpecification =
            @"@\(\s*
                (" + itemTypeOrMetadataNameSpecification + @")
                (\s*->\s*'[^']*')
                (\s*,\s*'[^']*')?
              \s*\)";

        // regular expression used to match item vector transforms, with no (named) capturing groups
        // internal for unit testing only
        internal static readonly Regex itemVectorTransformRawPattern = new Regex(itemVectorTransformRawSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        /**************************************************************************************************************************
         * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ItemExpander class.
         *************************************************************************************************************************/

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of this class using the specified TextWriter.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="w"></param>
        internal ProjectWriter(TextWriter w) : base(w)
        {
            documentEncoding = w.Encoding;
        }

        /// <summary>
        /// Creates an instance of this class using the specified stream and encoding.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="w"></param>
        /// <param name="encoding">If null, defaults to UTF-8 and omits encoding attribute from processing instruction.</param>
        internal ProjectWriter(Stream w, Encoding encoding) : base(w, encoding)
        {
            documentEncoding = encoding;
        }

        /// <summary>
        /// Creates an instance of this class using the specified file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="filename"></param>
        /// <param name="encoding">If null, defaults to UTF-8 and omits encoding attribute from processing instruction.</param>
        internal ProjectWriter(string filename, Encoding encoding) : base(filename, encoding)
        {
            documentEncoding = encoding;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes settings for the project to be saved.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="project"></param>
        /// <param name="projectXmlDeclaration">If null, XML declaration is not written.</param>
        internal void Initialize(XmlDocument project, XmlDeclaration projectXmlDeclaration)
        {
            // if the project's whitespace is not being preserved
            if (!project.PreserveWhitespace)
            {
                // write out child elements in an indented fashion, instead of jamming all the XML into one line
                base.Formatting = Formatting.Indented;
            }

            // don't write an XML declaration unless the project already has one or has non-default encoding
            writeXmlDeclaration = 
                ((projectXmlDeclaration != null) || 
                ((documentEncoding != Encoding.UTF8) && (documentEncoding != null)));
        }

        /// <summary>
        /// Writes the XML declaration only if the project being saved already has an XML declaration.
        /// </summary>
        /// <owner>SumedhK</owner>
        public override void WriteStartDocument()
        {
            if (writeXmlDeclaration)
            {
                base.WriteStartDocument();
            }
        }

        /// <summary>
        /// Writes the XML declaration only if the project being saved already has an XML declaration.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="standalone"></param>
        public override void WriteStartDocument(bool standalone)
        {
            if (writeXmlDeclaration)
            {
                base.WriteStartDocument(standalone);
            }
        }

        /// <summary>
        /// Writes item vector transforms embedded in the given string without escaping '->' into "-&amp;gt;".
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="text"></param>
        public override void WriteString(string text)
        {
            MatchCollection itemVectorTransforms = itemVectorTransformRawPattern.Matches(text);

            // if the string contains any item vector transforms
            if (itemVectorTransforms.Count > 0)
            {
                // separate out the text that surrounds the transforms
                // NOTE: use the Regex with no (named) capturing groups, otherwise Regex.Split() will split on them
                string[] surroundingTextPieces = itemVectorTransformRawPattern.Split(text);

                ErrorUtilities.VerifyThrow(itemVectorTransforms.Count == (surroundingTextPieces.Length - 1),
                    "We must have two pieces of surrounding text for every item vector transform found.");

                // write each piece of text before a transform, followed by the transform
                for (int i = 0; i < itemVectorTransforms.Count; i++)
                {
                    // write the text before the transform
                    base.WriteString(surroundingTextPieces[i]);

                    // break up the transform into its constituent pieces
                    Match itemVectorTransform = itemVectorTransformPattern.Match(itemVectorTransforms[i].Value);

                    ErrorUtilities.VerifyThrow(itemVectorTransform.Success,
                        "Item vector transform must be matched by both the raw and decorated regular expressions.");

                    // write each piece of the transform normally, except for the arrow -- write that without escaping
                    base.WriteString(itemVectorTransform.Groups["PREFIX"].Value);
                    base.WriteString(itemVectorTransform.Groups["TYPE"].Value);
                    base.WriteRaw(itemVectorTransform.Groups["ARROW"].Value);
                    base.WriteString(itemVectorTransform.Groups["TRANSFORM"].Value);
                    base.WriteString(itemVectorTransform.Groups["SEPARATOR_SPECIFICATION"].Value);
                    base.WriteString(itemVectorTransform.Groups["SUFFIX"].Value);
                }

                // write the terminal piece of text after the last transform
                base.WriteString(surroundingTextPieces[surroundingTextPieces.Length-1]);
            }
            // if the string has no item vector transforms in it, write it out as usual
            else
            {
                base.WriteString(text);
            }
        }

        #endregion

        // indicates whether an XML declaration e.g. <?xml version="1.0"?> will be written at the start of the project
        private bool writeXmlDeclaration;

        // encoding of the document, if specified when constructing
        private Encoding documentEncoding = null;
    }
}
