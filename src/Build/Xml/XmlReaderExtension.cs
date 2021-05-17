using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

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
        /// <param name="loadAsReadOnly">Whther to load the file in real only mode.</param>
        /// <returns>Disposable XmlReaderExtension object.</returns>
        internal static XmlReaderExtension Create(string filePath, bool loadAsReadOnly)
        {
            return new XmlReaderExtension(filePath, loadAsReadOnly);
        }

        private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly Stream _stream;
        private readonly StreamReader _streamReader;

        /// <summary>
        /// Caches a <see cref="PropertyInfo"/> representing the "Normalization" internal property on the <see cref="XmlReader"/>-derived
        /// type returned from <see cref="XmlReader.Create(TextReader, XmlReaderSettings, string)"/>. The cache is process/AppDomain-wide
        /// and lock-free, so we use volatile access for thread safety, i.e. to ensure that when the field is updated the PropertyInfo
        /// it's pointing to is seen as fully initialized by all CPUs.
        /// </summary>
        private static volatile PropertyInfo _normalizationPropertyInfo;

        private static bool _disableReadOnlyLoad;

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

        /// <summary>
        /// Returns <see cref="PropertyInfo"/> of the "Normalization" internal property on the given <see cref="XmlReader"/>-derived type.
        /// </summary>
        private static PropertyInfo GetNormalizationPropertyInfo(Type xmlReaderType)
        {
            PropertyInfo propertyInfo = _normalizationPropertyInfo;
            if (propertyInfo == null)
            {
                BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance;
                propertyInfo = xmlReaderType.GetProperty("Normalization", bindingFlags);
                _normalizationPropertyInfo = propertyInfo;
            }

            return propertyInfo;
        }

        private static XmlReader GetXmlReader(string file, StreamReader input, bool loadAsReadOnly, out Encoding encoding)
        {
            string uri = new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = file }.ToString();

            XmlReader reader = null;
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave16_10) && loadAsReadOnly && !_disableReadOnlyLoad)
            {
                // Create an XML reader with IgnoreComments and IgnoreWhitespace set if we know that we won't be asked
                // to write the DOM back to a file. This is a performance optimization.
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                };
                reader = XmlReader.Create(input, settings, uri);

                // Try to set Normalization to false. We do this to remain compatible with earlier versions of MSBuild
                // where we constructed the reader with 'new XmlTextReader()' which has normalization enabled by default.
                PropertyInfo normalizationPropertyInfo = GetNormalizationPropertyInfo(reader.GetType());
                if (normalizationPropertyInfo != null)
                {
                    normalizationPropertyInfo.SetValue(reader, false);
                }
                else
                {
                    // Fall back to using XmlTextReader if the prop could not be bound.
                    Debug.Fail("Could not set Normalization to false on the result of XmlReader.Create");
                    _disableReadOnlyLoad = true;

                    reader.Dispose();
                    reader = null;
                }
            }

            if (reader == null)
            {
                reader = new XmlTextReader(uri, input) { DtdProcessing = DtdProcessing.Ignore };
            }

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
