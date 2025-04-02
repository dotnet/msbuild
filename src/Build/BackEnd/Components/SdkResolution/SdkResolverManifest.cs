// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Serialization contract for an SDK Resolver manifest
    /// </summary>
    internal sealed class SdkResolverManifest
    {
        private SdkResolverManifest()
        {
        }

        public SdkResolverManifest(string DisplayName, string Path, Regex ResolvableSdkRegex)
        {
            this.DisplayName = DisplayName;
            this.Path = Path;
            this.ResolvableSdkRegex = ResolvableSdkRegex;
        }

        /// <summary>
        /// Sdk resolver manifest display name.
        /// </summary>
        /// <remarks>
        /// This field should be used only for logging purposes. Do not use for any actual processing, unless that are tests.
        /// </remarks>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Path for resolvers dll location.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Regex which matches all the sdk names that could be resolved by the resolvers associated with given manifest.
        /// </summary>
        public Regex ResolvableSdkRegex { get; private set; }

        /// <summary>
        /// The time-out interval for the name pattern regex in milliseconds.
        /// </summary>
        /// <remarks>
        /// This number should notify us when the name matching regex executes unreasonable amount of time (for example, have an infinite recursive regex expression).
        /// One should avoid to put such a regex into a resolver's xml and we want to catch this situation early. Half a second seems to be a reasonable time in which regex should finish.
        /// </remarks>
        private const int SdkResolverPatternRegexTimeoutMsc = 500;

        /// <summary>
        /// Deserialize the file into an SdkResolverManifest.
        /// </summary>
        /// <param name="filePath">Path to the manifest xml file.</param>
        /// <param name="manifestFolder">Path to the directory containing the manifest.</param>
        /// <returns>New deserialized collection instance.</returns>
        internal static SdkResolverManifest Load(string filePath, string manifestFolder)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings()
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (XmlReader reader = XmlReader.Create(stream, readerSettings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "SdkResolver")
                    {
                        SdkResolverManifest manifest = ParseSdkResolverElement(reader, filePath);

                        manifest.Path = FileUtilities.FixFilePath(manifest.Path);
                        if (!System.IO.Path.IsPathRooted(manifest.Path))
                        {
                            manifest.Path = System.IO.Path.Combine(manifestFolder, manifest.Path);
                            manifest.Path = System.IO.Path.GetFullPath(manifest.Path);
                        }

                        return manifest;
                    }
                    else
                    {
                        throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                    }
                }
            }

            return null;
        }

        // This parsing code is very specific and not forward compatible, but since resolvers generally ship in the same release vehicle as MSBuild itself, only backward compatibility is required.
        private static SdkResolverManifest ParseSdkResolverElement(XmlReader reader, string filePath)
        {
            SdkResolverManifest manifest = new SdkResolverManifest();
            manifest.DisplayName = filePath;

            reader.Read();
            while (!reader.EOF)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            switch (reader.Name)
                            {
                                case "Path":
                                    manifest.Path = reader.ReadElementContentAsString();
                                    break;
                                case "ResolvableSdkPattern":
                                    string pattern = reader.ReadElementContentAsString();
                                    try
                                    {
                                        RegexOptions regexOptions = RegexOptions.CultureInvariant;
                                        // For the kind of patterns used here, compiled regexes on .NET Framework tend to run slower than interpreted ones.
#if RUNTIME_TYPE_NETCORE
                                        regexOptions |= RegexOptions.Compiled;
#endif
                                        manifest.ResolvableSdkRegex = new Regex(pattern, regexOptions, TimeSpan.FromMilliseconds(SdkResolverPatternRegexTimeoutMsc));
                                    }
                                    catch (ArgumentException ex)
                                    {
                                        ErrorUtilities.ThrowInternalError("A regular expression parsing error occurred while parsing {0}.", ex, filePath);
                                    }
                                    break;
                                default:
                                    throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                            }
                            break;
                        }

                    case XmlNodeType.EndElement:
                        reader.Read();
                        break;

                    default:
                        throw new XmlException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedElement", reader.Name));
                }
            }

            return manifest;
        }
    }
}
