// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Shared XML helper methods used by XML-related tasks.
    /// </summary>
    internal static class XmlTaskUtility
    {
        /// <summary>
        /// Creates an <see cref="XmlReaderSettings"/> instance with the requested DTD behavior.
        /// </summary>
        /// <param name="prohibitDtd">True to prohibit DTDs; false to ignore DTDs.</param>
        /// <param name="closeInput">True to close the underlying input when the reader is disposed.</param>
        /// <returns>The configured reader settings.</returns>
        internal static XmlReaderSettings CreateReaderSettings(bool prohibitDtd, bool closeInput = false)
        {
            return new XmlReaderSettings
            {
                CloseInput = closeInput,
                DtdProcessing = prohibitDtd ? DtdProcessing.Prohibit : DtdProcessing.Ignore,
            };
        }

        /// <summary>
        /// Determines whether an exception is likely caused by prohibited DTD processing.
        /// </summary>
        /// <param name="exception">The thrown exception to inspect.</param>
        /// <param name="prohibitDtd">Whether DTDs were prohibited for the operation.</param>
        /// <param name="containsDtd">Callback that determines whether the input contains a DTD.</param>
        /// <returns>True when the failure is consistent with DTD prohibition.</returns>
        internal static bool IsDtdProhibitedException(Exception exception, bool prohibitDtd, Func<bool> containsDtd)
        {
            if (!prohibitDtd || !ContainsXmlException(exception))
            {
                return false;
            }

            try
            {
                return containsDtd();
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
            {
                // If we cannot re-read/inspect input, don't classify as DTD-prohibited.
                return false;
            }
        }

        /// <summary>
        /// Determines whether XML text contains a DOCTYPE declaration.
        /// </summary>
        /// <param name="xml">The XML text to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        internal static bool ContainsDtd(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return false;
            }

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0
                };

                using (var reader = XmlReader.Create(new System.IO.StringReader(xml), settings))
                {
                    return ContainsDtd(reader);
                }
            }
            catch (XmlException)
            {
                // Let the actual XML parsing handle the malformed XML error.
                return false;
            }
        }

        /// <summary>
        /// Determines whether the XML file contains a DOCTYPE declaration.
        /// </summary>
        /// <param name="filePath">The XML file path to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        internal static bool ContainsDtd(AbsolutePath filePath)
        {
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Parse,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 0
                };

                using (var stream = File.OpenRead(filePath.Value))
                using (var reader = XmlReader.Create(stream, settings, filePath.Value))
                {
                    return ContainsDtd(reader);
                }
            }
            catch (XmlException)
            {
                // Let the actual XML parsing handle the malformed XML error.
                return false;
            }
        }

        /// <summary>
        /// Walks the exception chain and checks whether an <see cref="XmlException"/> is present.
        /// </summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns>True when an XML exception is found in the chain.</returns>
        private static bool ContainsXmlException(Exception exception)
        {
            for (Exception current = exception; current is not null; current = current.InnerException)
            {
                if (current is XmlException)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDtd(XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.DocumentType)
                {
                    return true;
                }

                if (reader.NodeType == XmlNodeType.Element)
                {
                    // A DOCTYPE declaration must appear before the root element.
                    return false;
                }
            }

            return false;
        }
    }
}
