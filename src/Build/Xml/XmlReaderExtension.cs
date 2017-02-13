using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

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
        /// <returns>Disposable XmlReaderExtenion object.</returns>
        internal static XmlReaderExtension Create(string filePath)
        {
            return new XmlReaderExtension(filePath);
        }

        private readonly Encoding _encoding;
        private readonly Stream _stream;
        private readonly StreamReader _streamReader;

        private XmlReaderExtension(string file)
        {
            try
            {
                _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                _streamReader = new StreamReader(_stream, Encoding.UTF8, true);
                Reader = GetXmlReader(_streamReader, out _encoding);

                // Override detected encoding if xml encoding attribute is specified
                var encodingAttribute = Reader.GetAttribute("encoding");
                _encoding = !string.IsNullOrEmpty(encodingAttribute)
                    ? Encoding.GetEncoding(encodingAttribute)
                    : _encoding;
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

        internal Encoding Encoding => _encoding;

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
    }
}
