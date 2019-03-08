using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal
{
    /// <summary>
    ///     Disposable helper class to wrap XmlReader / XmlTextReader functionality.
    /// </summary>
    internal class XmlReaderExtension : IDisposable
    {
        /// <summary>
        ///     Creates an XmlReaderExtension with handle to an XmlReader.
        /// </summary>
        /// <param name="filePath">Path to the file on disk.</param>
        /// <returns>Disposable XmlReaderExtension object.</returns>
        internal static XmlReaderExtension Create(string filePath, bool loadAsReadOnly)
        {
            return new XmlReaderExtension(filePath, loadAsReadOnly);
        }

        private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly Stream _stream;
        private readonly StreamReader _streamReader;

        private XmlReaderExtension(string file, bool loadAsReadOnly)
        {
            try
            {
                // Note: Passing in UTF8 w/o BOM into StreamReader. If the BOM is detected StreamReader will set the
                // Encoding correctly (detectEncodingFromByteOrderMarks = true). The default is to use UTF8 (with BOM)
                // which will cause the BOM to be added when we re-save the file in cases where it was not present on
                // load.
                _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                _streamReader = new StreamReader(_stream, s_utf8NoBom, detectEncodingFromByteOrderMarks: true);
                Encoding detectedEncoding;

                // The XmlDocumentWithWithLocation class relies on the reader's BaseURI property to be set,
                // thus we pass the document's file path to the appropriate xml reader constructor.
                Reader = GetXmlReader(file, _streamReader, loadAsReadOnly, out detectedEncoding);

                // Override detected encoding if an XML encoding attribute is specified and that encoding is sufficiently
                // different from the detected encoding.
                // Note: Using SimilarToEncoding to ensure that if the encoding is specified "utf-8" but the detected
                // encoding is UTF w/o BOM use the detected encoding and not utf-8 which will add a BOM on save.
                var encodingFromAttribute = GetEncodingFromAttribute(Reader);
                Encoding = encodingFromAttribute != null && !detectedEncoding.SimilarToEncoding(encodingFromAttribute)
                    ? encodingFromAttribute
                    : detectedEncoding;
            }
            catch
            {
                // GetXmlReader calls Read() to get Encoding and can throw. If it does, close 
                // the streams as needed.
                Dispose();
                throw;
            }
        }

        internal XmlReader Reader { get; }

        internal Encoding Encoding { get; }

        public void Dispose()
        {
            Reader?.Dispose();
            _streamReader?.Dispose();
            _stream?.Dispose();
        }

        private static XmlReader GetXmlReader(string file, StreamReader input, bool loadAsReadOnly, out Encoding encoding)
        {
            string uri = new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = file }.ToString();

            XmlReader reader;

            // Ignore loadAsReadOnly for now; using XmlReader.Create results in whitespace changes
            // of attribute text, specifically newline removal.
            // https://github.com/Microsoft/msbuild/issues/4210
            reader = new XmlTextReader(uri, input) { DtdProcessing = DtdProcessing.Ignore };

            reader.Read();
            encoding = input.CurrentEncoding;

            return reader;
        }

        /// <summary>
        /// Get the Encoding type from the XML declaration tag
        /// </summary>
        /// <param name="reader">XML Reader object</param>
        /// <returns>Encoding if specified, else null.</returns>
        private static Encoding GetEncodingFromAttribute(XmlReader reader)
        {
            var encodingAttributeString = reader.GetAttribute("encoding");

            return !string.IsNullOrEmpty(encodingAttributeString)
                ? Encoding.GetEncoding(encodingAttributeString)
                : null;
        }
    }
}
