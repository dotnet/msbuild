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
        /// The DOCTYPE declaration marker to search for in XML content.
        /// </summary>
        private const string DoctypeMarker = "<!DOCTYPE";

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
        /// Determines whether XML text contains a DOCTYPE declaration using text-based search.
        /// </summary>
        /// <param name="xml">The XML text to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        internal static bool ContainsDtd(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return false;
            }

            // Simple text-based detection - no XML parsing required.
            // DOCTYPE declaration syntax is fixed, so string search is safe and efficient.
            return xml.Contains(DoctypeMarker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the XML file contains a DOCTYPE declaration using text-based search.
        /// </summary>
        /// <param name="filePath">The XML file path to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        internal static bool ContainsDtd(AbsolutePath filePath)
        {
            try
            {
                // Read only the beginning of the file - DOCTYPE must appear before the root element.
                // 8KB should be more than enough to find it in any reasonable XML file.
                using var stream = File.OpenRead(filePath.Value);
                using var reader = new StreamReader(stream);
                var buffer = new char[8192];
                int charsRead = reader.Read(buffer, 0, buffer.Length);
                var content = new string(buffer, 0, charsRead);
                return content.Contains(DoctypeMarker, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If we cannot read the file, don't classify as containing DTD.
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
    }
}
