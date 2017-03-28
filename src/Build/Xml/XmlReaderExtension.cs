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
        internal static XmlReaderExtension Create(string filePath)
        {
            return new XmlReaderExtension(filePath);
        }

        private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly Stream _stream;
        private readonly StreamReader _streamReader;

        private XmlReaderExtension(string file)
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
                Reader = GetXmlReader(_streamReader, out detectedEncoding);

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

        private static XmlReader GetXmlReader(StreamReader input, out Encoding encoding)
        {
#if FEATURE_XMLTEXTREADER
            var reader = new XmlTextReader(input) { DtdProcessing = DtdProcessing.Ignore };

            reader.Read();
            encoding = input.CurrentEncoding;

            return reader;
#else
            var xr = XmlReader.Create(input, new XmlReaderSettings {DtdProcessing = DtdProcessing.Ignore});

            // Set Normalization = false if possible. Without this, certain line endings will be normalized
            // with \n (specifically in XML comments). Does not throw if if type or property is not found.
            // This issue does not apply to XmlTextReader (above) which is not shipped with .NET Core yet.
            
            // NOTE: This doesn't work in .NET Core.
            //var xmlReaderType = typeof(XmlReader).GetTypeInfo().Assembly.GetType("System.Xml.XmlTextReaderImpl");

            //// Works in full framework, not in .NET Core
            //var normalization = xmlReaderType?.GetProperty("Normalization", BindingFlags.Instance | BindingFlags.NonPublic);
            //normalization?.SetValue(xr, false);

            //// Set _normalize = false, and _ps.eolNormalized = true
            //var normalizationMember = xmlReaderType?.GetField("_normalize", BindingFlags.Instance | BindingFlags.NonPublic);
            //normalizationMember?.SetValue(xr, false);

            //var psField = xmlReaderType.GetField("_ps", BindingFlags.Instance | BindingFlags.NonPublic);
            //var ps = psField.GetValue(xr);
            
            //var eolField = ps.GetType().GetField("eolNormalized", BindingFlags.Instance | BindingFlags.NonPublic);
            //eolField.SetValue(ps, true);

            xr.Read();
            encoding = input.CurrentEncoding;

            return xr;
#endif
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
