// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Read information from application .config files.
    /// </summary>
    internal sealed class AppConfig
    {
        /// <summary>
        /// Read the .config from a file.
        /// </summary>
        /// <param name="appConfigFile"></param>
        internal void Load(string appConfigFile)
        {
            XmlReader reader = null;
            try
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                reader = XmlReader.Create(appConfigFile, readerSettings);
                Read(reader);
            }
            catch (XmlException e)
            {
                int lineNumber = 0;
                int linePosition = 0;

                if (reader is IXmlLineInfo info)
                {
                    lineNumber = info.LineNumber;
                    linePosition = info.LinePosition;
                }

                throw new AppConfigException(e.Message, appConfigFile, lineNumber, linePosition, e);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                int lineNumber = 0;
                int linePosition = 0;

                if (reader is IXmlLineInfo info)
                {
                    lineNumber = info.LineNumber;
                    linePosition = info.LinePosition;
                }

                throw new AppConfigException(e.Message, appConfigFile, lineNumber, linePosition, e);
            }
            finally
            {
                reader?.Dispose();
            }
        }

        /// <summary>
        /// Read the .config from an XmlReader
        /// </summary>
        /// <param name="reader"></param>
        internal void Read(XmlReader reader)
        {
            // Read the app.config XML
            while (reader.Read())
            {
                // Look for the <runtime> section
                if (reader.NodeType == XmlNodeType.Element && StringEquals(reader.Name, "runtime"))
                {
                    Runtime.Read(reader);
                }
            }
        }

        /// <summary>
        /// Access the Runtime section of the application .config file.
        /// </summary>
        internal RuntimeSection Runtime { get; } = new RuntimeSection();

        /// <summary>
        /// App.config files seem to come with mixed casing for element and attribute names.
        /// If the fusion loader can handle this then this code should too.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        internal static bool StringEquals(string a, string b)
        {
            return String.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
