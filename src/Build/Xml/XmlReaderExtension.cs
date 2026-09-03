// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

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
        /// <param name="sourceLoadCapture">Optional failed-source observation state.</param>
        /// <returns>Disposable XmlReaderExtension object.</returns>
        internal static XmlReaderExtension Create(
            string filePath,
            bool loadAsReadOnly,
            EvaluationProjectSourceLoadCapture sourceLoadCapture = null)
        {
            return new XmlReaderExtension(filePath, loadAsReadOnly, sourceLoadCapture);
        }

        private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly Stream _stream;
        private readonly StreamReader _streamReader;
        private readonly HashingReadStream _hashingStream;
        private readonly EvaluationProjectSourceLoadCapture _sourceLoadCapture;

        private XmlReaderExtension(
            string file,
            bool loadAsReadOnly,
            EvaluationProjectSourceLoadCapture sourceLoadCapture)
        {
            _sourceLoadCapture = sourceLoadCapture;
            try
            {
                // Note: Passing in UTF8 w/o BOM into StreamReader. If the BOM is detected StreamReader will set the
                // Encoding correctly (detectEncodingFromByteOrderMarks = true). The default is to use UTF8 (with BOM)
                // which will cause the BOM to be added when we re-save the file in cases where it was not present on
                // load.
                Stream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (EvaluationObservationSession.IsEnabled)
                {
                    _hashingStream = new HashingReadStream(fileStream);
                    _stream = _hashingStream;
                }
                else
                {
                    _stream = fileStream;
                }

                _streamReader = new StreamReader(_stream, s_utf8NoBom, detectEncodingFromByteOrderMarks: true);
                Encoding detectedEncoding;

#if RUNTIME_TYPE_NETCORE
                // Ensure that all Windows codepages are available.
                // Safe to call multiple times per https://docs.microsoft.com/en-us/dotnet/api/system.text.encoding.registerprovider
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

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
                try
                {
                    CaptureFailureObservation();
                }
                finally
                {
                    Dispose();
                }

                throw;
            }
        }

        internal XmlReader Reader { get; }

        internal Encoding Encoding { get; }

        internal string ContentHash => _hashingStream?.GetContentHash();

        internal void CaptureFailureObservation()
        {
            if (_sourceLoadCapture is null)
            {
                return;
            }

            try
            {
                _sourceLoadCapture.ContentHash = _hashingStream?.CompleteContentHash();
                _sourceLoadCapture.Encoding =
                    (Encoding ?? _streamReader?.CurrentEncoding)?.WebName;
                _sourceLoadCapture.ContentCaptureFailed =
                    _hashingStream is not null &&
                    _sourceLoadCapture.ContentHash is null;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                _sourceLoadCapture.ContentCaptureFailed = true;
            }
        }

        public void Dispose()
        {
            Reader?.Dispose();
            _streamReader?.Dispose();
            _stream?.Dispose();
        }

        private static XmlReader GetXmlReader(string file, StreamReader input, bool loadAsReadOnly, out Encoding encoding)
        {
            string uri = new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = file }.ToString();


            // loadAsReadOnly is currently ignored.
            // Compatibility note: XmlReader.Create normalizes whitespace/newlines in ways that changed
            // observed project values (for example, multiline Exec commands and metadata), breaking
            // existing builds. We intentionally keep XmlTextReader behavior here to preserve those
            // established semantics until a non-reflection, compatibility-safe replacement exists.
            // Related history: #4210, #4213, #4083, #6232, #6669.
            XmlReader reader = new XmlTextReader(uri, input) { DtdProcessing = DtdProcessing.Ignore };

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

        private sealed class HashingReadStream : Stream
        {
            private readonly Stream _inner;
            private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            private readonly byte[] _singleByteBuffer = new byte[1];
            private string _contentHash;
            private bool _sequential = true;

            internal HashingReadStream(Stream inner)
            {
                _inner = inner;
            }

            internal string GetContentHash()
            {
                if (!_sequential)
                {
                    return null;
                }

                if (_inner.CanSeek && _inner.Position != _inner.Length)
                {
                    return null;
                }

                if (_contentHash is null)
                {
                    _contentHash = Convert.ToBase64String(_hash.GetHashAndReset());
                }

                return _contentHash;
            }

            internal string CompleteContentHash()
            {
                if (!_sequential)
                {
                    return null;
                }

                if (_contentHash is not null)
                {
                    return _contentHash;
                }

                byte[] buffer = new byte[4096];
                while (Read(buffer, 0, buffer.Length) > 0)
                {
                }

                return GetContentHash();
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set
                {
                    if (value != _inner.Position)
                    {
                        _sequential = false;
                    }

                    _inner.Position = value;
                }
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                if (read > 0 && _sequential)
                {
                    _hash.AppendData(buffer, offset, read);
                }

                return read;
            }

            public override int ReadByte()
            {
                int value = _inner.ReadByte();
                if (value >= 0 && _sequential)
                {
                    _singleByteBuffer[0] = (byte)value;
                    _hash.AppendData(_singleByteBuffer);
                }

                return value;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                _sequential = false;
                return _inner.Seek(offset, origin);
            }

            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _hash.Dispose();
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
