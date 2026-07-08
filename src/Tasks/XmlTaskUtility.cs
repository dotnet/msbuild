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
            return prohibitDtd && ContainsXmlException(exception) && containsDtd();
        }

        /// <summary>
        /// Determines whether XML text contains a DOCTYPE declaration.
        /// </summary>
        /// <param name="xml">The XML text to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        /// <remarks>This method does not throw.</remarks>
        internal static bool ContainsDtd(string xml)
        {
            return !string.IsNullOrEmpty(xml) && xml.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Determines whether the XML file contains a DOCTYPE declaration.
        /// </summary>
        /// <param name="filePath">The XML file path to inspect.</param>
        /// <returns>True when a DOCTYPE declaration is present.</returns>
        internal static bool ContainsDtd(AbsolutePath filePath)
        {
            return ContainsDtd(File.ReadAllText(filePath.Value));
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
