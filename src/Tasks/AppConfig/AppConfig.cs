// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.Shared;

#nullable disable

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
        /// <param name="appConfigFilePath"></param>
        internal void Load(string appConfigFilePath)
        {
            XmlReader reader = null;
            try
            {
                var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, CloseInput = true};

                // it's important to normalize the path as it may contain two slashes
                // see https://github.com/dotnet/msbuild/issues/4335 for details.
                appConfigFilePath = FileUtilities.NormalizePath(appConfigFilePath);

                // Need a filestream as the XmlReader doesn't support nonstandard unicode characters in path.
                // No need to dispose - as 'CloseInput' was passed to XmlReaderSettings
                FileStream fs = File.OpenRead(appConfigFilePath);
                reader = XmlReader.Create(fs, readerSettings);
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

                throw new AppConfigException(e.Message, appConfigFilePath, lineNumber, linePosition, e);
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

                throw new AppConfigException(e.Message, appConfigFilePath, lineNumber, linePosition, e);
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
            return String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
