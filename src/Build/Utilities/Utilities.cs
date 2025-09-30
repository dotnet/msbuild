// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
#if NET
using System.IO;
#else
using Microsoft.IO;
#endif
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Toolset = Microsoft.Build.Evaluation.Toolset;
using XmlElementWithLocation = Microsoft.Build.Construction.XmlElementWithLocation;

#nullable disable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    internal static partial class Utilities
    {
        /// <summary>
        /// Save off the contents of the environment variable that specifies whether we should treat higher toolsversions as the current
        /// toolsversion.  (Some hosts require this.)
        /// </summary>
        private static bool s_shouldTreatHigherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATHIGHERTOOLSVERSIONASCURRENT") != null);

        /// <summary>
        /// Save off the contents of the environment variable that specifies whether we should treat all toolsversions, regardless of
        /// whether they are higher or lower, as the current toolsversion.  (Some hosts require this.)
        /// </summary>
        private static bool s_shouldTreatOtherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATALLTOOLSVERSIONSASCURRENT") != null);

        /// <summary>
        /// If set, default to the ToolsVersion from the project file (or if that doesn't isn't set, default to 2.0).  Otherwise, use Dev12+
        /// defaulting logic: first check the MSBUILDDEFAULTTOOLSVERSION environment variable, then check for a DefaultOverrideToolsVersion,
        /// then if both fail, use the current ToolsVersion.
        /// </summary>
        private static bool s_uselegacyDefaultToolsVersionBehavior = (Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION") != null);

        /// <summary>
        /// If set, will be used as the ToolsVersion to build with (unless MSBUILDLEGACYDEFAULTTOOLSVERSION is set).
        /// </summary>
        private static string s_defaultToolsVersionFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDDEFAULTTOOLSVERSION");

        /// <summary>
        /// Delegate for a method that, given a ToolsVersion string, returns the matching Toolset.
        /// </summary>
        internal delegate Toolset GetToolset(string toolsVersion);

        /// <summary>
        /// INTERNAL FOR UNIT-TESTING ONLY
        ///
        /// We've got several environment variables that we read into statics since we don't expect them to ever
        /// reasonably change, but we need some way of refreshing their values so that we can modify them for
        /// unit testing purposes.
        /// </summary>
        internal static void RefreshInternalEnvironmentValues()
        {
            s_shouldTreatHigherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATHIGHERTOOLSVERSIONASCURRENT") != null);
            s_shouldTreatOtherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATALLTOOLSVERSIONSASCURRENT") != null);
            s_uselegacyDefaultToolsVersionBehavior = (Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION") != null);
            s_defaultToolsVersionFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDDEFAULTTOOLSVERSION");
        }

        /// <summary>
        /// Sets the inner XML/text of the given XML node, escaping as necessary.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="s">Can be empty string, but not null.</param>
        internal static void SetXmlNodeInnerContents(XmlElementWithLocation node, string s)
        {
            ErrorUtilities.VerifyThrow(s != null, "Need value to set.");

            if (s.Contains('<'))
            {
                // If the value looks like it probably contains XML markup ...
                try
                {
                    // Attempt to store it verbatim as XML.
                    node.InnerXml = s;
                    return;
                }
                catch (XmlException)
                {
                    // But that may fail, in the event that "s" is not really well-formed
                    // XML.  Eat the exception and fall through below ...
                }
            }

            // The value does not contain valid XML markup.  Store it as text, so it gets
            // escaped properly.
            node.InnerText = s;
        }

        /// <summary>
        /// Extracts the inner XML/text of the given XML node, unescaping as necessary.
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Inner XML/text of specified node.</returns>
        internal static string GetXmlNodeInnerContents(XmlElementWithLocation node)
        {
            // XmlNode.InnerXml gives back a string that consists of the set of characters
            // in between the opening and closing elements of the XML node, without doing any
            // unescaping.  Any "strange" character sequences (like "<![CDATA[...]]>" will remain
            // exactly so and will not be translated or interpreted.  The only modification that
            // .InnerXml will do is that it will normalize any Xml contained within.  This means
            // normalizing whitespace between XML attributes and quote characters that surround XML
            // attributes.  If PreserveWhitespace is false, then it will also normalize whitespace
            // between elements.
            //
            // XmlNode.InnerText strips out any Xml contained within, and then unescapes the rest
            // of the text.  So if the remaining text contains certain character sequences such as
            // "&amp;" or "<![CDATA[...]]>", these will be translated into their equivalent representations.
            //
            // It's hard to explain, but much easier to demonstrate with examples:
            //
            // Original XML                     XmlNode.InnerText               XmlNode.InnerXml
            // ===========================      ==============================  ======================================
            //
            // <a><![CDATA[whatever]]></a>      whatever                        <![CDATA[whatever]]>
            //
            // <a>123<MyNode/>456</a>           123456                          123<MyNode />456
            //
            // <a>123456</a>                    123456                          123456
            //
            // <a>123<MyNode b='&lt;'/>456</a>  123456                          123<MyNode b="&lt;" />456
            //
            // <a>123&amp;456</a>               123&456                         123&amp;456

            // So the trick for MSBuild when interpreting a property value is to know which one to
            // use ... InnerXml or InnerText.  There are two basic scenarios we care about.
            //
            // 1.)  The first scenario is that the user is trying to create a property whose
            //      contents are actually XML.  That is to say that the contents may be written
            //      to a XML file, or may be passed in as a string to XmlDocument.LoadXml.
            //      In this case, we would want to use XmlNode.InnerXml, because we DO NOT want
            //      character sequences to be unescaped.  If we did unescape them, then whatever
            //      XML parser tried to read in the stream as XML later on would totally barf.
            //
            // 2.)  The second scenario is the the user is trying to create a property that
            //      is just intended to be treated as a string.  That string may be very large
            //      and could contain all sorts of whitespace, carriage returns, special characters,
            //      etc.  But in the end, it's just a big string.  In this case, whatever
            //      task is actually processing this string ... it's not going to know anything
            //      about character sequences such as &amp; and &lt;.  These character sequences
            //      are specific to XML markup.  So, here we want to use XmlNode.InnerText so that
            //      the character sequences get unescaped into their actual character before
            //      the string is passed to the task (or wherever else the property is used).
            //      Of course, if the string value of the property needs to contain characters
            //      like <, >, &, etc., then the user must XML escape these characters otherwise
            //      the XML parser reading the project file will croak.  Or if the user doesn't
            //      want to escape every instance of these characters, he can surround the whole
            //      thing with a CDATA tag.  Again, if he does this, we don't want the task to
            //      receive the C, D, A, T, A as part of the string ... this should be stripped off.
            //      Again, using XmlNode.InnerText takes care of this.
            //
            // 2b.) A variation of the second scenario is that the user is trying to create a property
            //      that is just intended to be a string, but wants to comment out part of the string.
            //      For example, it's a semicolon separated list that's going ultimately to end up in a list.
            //      eg. (DDB #56841)
            //
            //     <BuildDirectories>
            //        <!--
            //              env\TestTools\tshell\pkg;
            //        -->
            //                ndp\fx\src\VSIP\FrameWork;
            //                ndp\fx\src\xmlTools;
            //                ddsuites\src\vs\xmlTools;
            //     </BuildDirectories>
            //
            //      In this case, we want to treat the string as text, so that we don't retrieve the comment.
            //      We only want to retrieve the comment if there's some other XML in there. The
            //      mere presence of an XML comment shouldn't make us think the value is XML.
            //
            // Given these two scenarios, how do we know whether the user intended to treat
            // a property value as XML or text?  We use a simple heuristic which is that if
            // XmlNode.InnerXml contains any "<" characters, then there pretty much has to be
            // XML in there, so we'll just use XmlNode.InnerXml.  If there are no "<" characters that aren't merely comments,
            // then we assume it's to be treated as text and we use XmlNode.InnerText.  Also, if
            // it looks like the whole thing is one big CDATA block, then we also use XmlNode.InnerText.

            // XmlNode.InnerXml is much more expensive than InnerText. Don't use it for trivial cases.
            // (single child node with a trivial value or no child nodes)
            if (!node.HasChildNodes || (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Whitespace))
            {
                return String.Empty;
            }

            if (node.ChildNodes.Count == 1 && (node.FirstChild.NodeType == XmlNodeType.Text || node.FirstChild.NodeType == XmlNodeType.CDATA))
            {
                return node.InnerText;
            }

            string innerXml = node.InnerXml;

            // If there is no markup under the XML node (detected by the presence
            // of a '<' sign
            int firstLessThan = innerXml.IndexOf('<');
            if (firstLessThan == -1)
            {
                // return the inner text so it gets properly unescaped
                return node.InnerText;
            }

            bool containsNoTagsOtherThanComments = ContainsNoTagsOtherThanComments(innerXml, firstLessThan);

            // ... or if the only XML is comments,
            if (containsNoTagsOtherThanComments)
            {
                // return the inner text so the comments are stripped
                // (this is how one might comment out part of a list in a property value)
                return node.InnerText;
            }

            // ...or it looks like the whole thing is a big CDATA tag ...
            bool startsWithCData = innerXml.AsSpan().TrimStart().StartsWith("<![CDATA[".AsSpan(), StringComparison.Ordinal);

            if (startsWithCData)
            {
                // return the inner text so it gets properly extracted from the CDATA
                return node.InnerText;
            }

            // otherwise, it looks like genuine XML; return the inner XML so that
            // tags and comments are preserved and any XML escaping is preserved
            return innerXml;
        }

        /// <summary>
        /// Figure out whether there are any XML tags, other than comment tags,
        /// in the string.
        /// </summary>
        /// <remarks>
        /// We know the string coming in is a valid XML fragment. (The project loaded after all.)
        /// So for example we can ignore an open comment tag without a matching closing comment tag.
        /// </remarks>
        private static bool ContainsNoTagsOtherThanComments(string innerXml, int firstLessThan)
        {
            bool insideComment = false;
            for (int i = firstLessThan; i < innerXml.Length; i++)
            {
                if (!insideComment)
                {
                    // XML comments start with exactly "<!--"
                    if (i < innerXml.Length - 3
                        && innerXml[i] == '<'
                        && innerXml[i + 1] == '!'
                        && innerXml[i + 2] == '-'
                        && innerXml[i + 3] == '-')
                    {
                        // Found the start of a comment
                        insideComment = true;
                        i += 3;
                        continue;
                    }
                }

                if (!insideComment)
                {
                    if (innerXml[i] == '<')
                    {
                        // Found a tag!
                        return false;
                    }
                }

                if (insideComment)
                {
                    // XML comments end with exactly "-->"
                    if (i < innerXml.Length - 2
                        && innerXml[i] == '-'
                        && innerXml[i + 1] == '-'
                        && innerXml[i + 2] == '>')
                    {
                        // Found the end of a comment
                        insideComment = false;
                        i += 2;
                        continue;
                    }
                }
            }

            // Didn't find any tags, except possibly comments
            return true;
        }

        // used to find the xmlns attribute
#if NET
        [GeneratedRegex("xmlns=\"[^\"]*\"\\s*")]
        private static partial Regex XmlnsPattern { get; }
#else
        private static Regex XmlnsPattern { get; } = new Regex("xmlns=\"[^\"]*\"\\s*");
#endif

        /// <summary>
        /// Removes the xmlns attribute from an XML string.
        /// </summary>
        /// <param name="xml">XML string to process.</param>
        /// <returns>The modified XML string.</returns>
        internal static string RemoveXmlNamespace(string xml)
        {
            return XmlnsPattern.Replace(xml, String.Empty);
        }

        /// <summary>
        /// Creates a comma separated list of valid tools versions suitable for an error message.
        /// </summary>
        internal static string CreateToolsVersionListString(IEnumerable<Toolset> toolsets)
        {
            StringBuilder sb = StringBuilderCache.Acquire();

            foreach (Toolset toolset in toolsets)
            {
                if (sb.Length != 0)
                {
                    sb.Append(", ");
                }

                sb.Append('"').Append(toolset.ToolsVersion).Append('"');
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Figure out what ToolsVersion to use to actually build the project with.
        /// </summary>
        /// <param name="explicitToolsVersion">The user-specified ToolsVersion (through e.g. /tv: on the command line)</param>
        /// <param name="toolsVersionFromProject">The ToolsVersion from the project file</param>
        /// <param name="getToolset">Delegate used to test whether a toolset exists for a given ToolsVersion.  May be null, in which
        /// case we act as though that toolset existed.</param>
        /// <param name="defaultToolsVersion">The default ToolsVersion</param>
        /// <param name="usingDifferentToolsVersionFromProjectFile">true if the project file specifies an explicit toolsversion but a different one is chosen</param>
        /// <returns>The ToolsVersion we should use to build this project.  Should never be null.</returns>
        internal static string GenerateToolsVersionToUse(string explicitToolsVersion, string toolsVersionFromProject, GetToolset getToolset, string defaultToolsVersion, out bool usingDifferentToolsVersionFromProjectFile)
        {
            string toolsVersionToUse = explicitToolsVersion;

            // hosts may need to treat toolsversions later than the current one as the current one ... or may just
            // want to treat all toolsversions as though they're the current one, so give them that ability
            // through an environment variable
            if (s_shouldTreatOtherToolsVersionsAsCurrent)
            {
                toolsVersionToUse = MSBuildConstants.CurrentToolsVersion;
            }
            else
            {
                if (s_shouldTreatHigherToolsVersionsAsCurrent)
                {
                    if (Version.TryParse(toolsVersionFromProject, out var toolsVersionAsVersion))
                    {
                        // This is higher than the 'legacy' toolsversion values.
                        // Therefore we need to enter best effort mode and
                        // present the current one.
                        if (toolsVersionAsVersion > new Version(15, 0))
                        {
                            toolsVersionToUse = MSBuildConstants.CurrentToolsVersion;
                        }
                    }
                }

                // If ToolsVersion has not either been explicitly set or been overridden via one of the methods
                // mentioned above
                if (toolsVersionToUse == null)
                {
                    // We want to generate the ToolsVersion based on the legacy behavior if EITHER:
                    // - the environment variable (MSBUILDLEGACYDEFAULTTOOLSVERSION) is set
                    // - the current ToolsVersion doesn't actually exist.  This is extremely unlikely
                    //   to happen normally, but may happen in checked-in toolset scenarios, in which
                    //   case we want to make sure we're at least as tolerant as Dev11 was.
                    Toolset currentToolset = null;

                    if (getToolset != null)
                    {
                        currentToolset = getToolset(MSBuildConstants.CurrentToolsVersion);
                    }

                    // if we want to do the legacy behavior, act as we did through Dev11:
                    // - If project file defines a ToolsVersion that has a valid toolset associated with it, use that
                    // - Otherwise, if project file defines an invalid ToolsVersion, use the current ToolsVersion
                    // - Otherwise, if project file does not define a ToolsVersion, use the default ToolsVersion (must
                    //   be "2.0" since 2.0 projects did not have a ToolsVersion field).
                    if (s_uselegacyDefaultToolsVersionBehavior || (getToolset != null && currentToolset == null))
                    {
                        if (!String.IsNullOrEmpty(toolsVersionFromProject))
                        {
                            toolsVersionToUse = toolsVersionFromProject;

                            // If we can tell that the toolset specified in the project is not present
                            // then we'll use the current version.  Otherwise, we'll assume our caller
                            // knew what it was doing.
                            if (getToolset != null && getToolset(toolsVersionToUse) == null)
                            {
                                toolsVersionToUse = MSBuildConstants.CurrentToolsVersion;
                            }
                        }
                        else
                        {
                            toolsVersionToUse = defaultToolsVersion;
                        }
                    }
                    else
                    {
                        // Otherwise, first check to see if the default ToolsVersion has been set in the environment.
                        // Ideally we'll check to make sure it's a valid ToolsVersion, but if we don't have the ability
                        // to do so, we'll assume the person who set the environment variable knew what they were doing.
                        if (!String.IsNullOrEmpty(s_defaultToolsVersionFromEnvironment))
                        {
                            if (getToolset == null || getToolset(s_defaultToolsVersionFromEnvironment) != null)
                            {
                                toolsVersionToUse = s_defaultToolsVersionFromEnvironment;
                            }
                        }

                        // Otherwise, check to see if the override default toolsversion from the toolset works.  Though
                        // it's attached to the Toolset, it's actually MSBuild version dependent, so any loaded Toolset
                        // should have the same one.
                        //
                        // And if that doesn't work, then just fall back to the current ToolsVersion.
                        if (toolsVersionToUse == null)
                        {
                            if (getToolset != null && currentToolset != null)
                            {
                                string defaultOverrideToolsVersion = currentToolset.DefaultOverrideToolsVersion;

                                if (!String.IsNullOrEmpty(defaultOverrideToolsVersion) && getToolset(defaultOverrideToolsVersion) != null)
                                {
                                    toolsVersionToUse = defaultOverrideToolsVersion;
                                }
                                else
                                {
                                    toolsVersionToUse = MSBuildConstants.CurrentToolsVersion;
                                }
                            }
                            else
                            {
                                toolsVersionToUse = MSBuildConstants.CurrentToolsVersion;
                            }
                        }
                    }
                }
            }

            ErrorUtilities.VerifyThrow(!String.IsNullOrEmpty(toolsVersionToUse), "Should always return a ToolsVersion");

            var explicitToolsVersionSpecified = explicitToolsVersion != null;
            usingDifferentToolsVersionFromProjectFile = UsingDifferentToolsVersionFromProjectFile(toolsVersionFromProject, toolsVersionToUse, explicitToolsVersionSpecified);

            return toolsVersionToUse;
        }

        private static bool UsingDifferentToolsVersionFromProjectFile(string toolsVersionFromProject, string toolsVersionToUse, bool explicitToolsVersionSpecified)
        {
            return !explicitToolsVersionSpecified &&
                    !String.IsNullOrEmpty(toolsVersionFromProject) &&
                    !String.Equals(toolsVersionFromProject, toolsVersionToUse, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieves properties derived from the current
        /// environment variables.
        /// </summary>
        internal static PropertyDictionary<ProjectPropertyInstance> GetEnvironmentProperties(bool makeReadOnly)
        {
            IDictionary<string, string> environmentVariablesBag = CommunicationsUtilities.GetEnvironmentVariables();

            var envPropertiesHashSet = new RetrievableValuedEntryHashSet<ProjectPropertyInstance>(environmentVariablesBag.Count + 2, MSBuildNameIgnoreCaseComparer.Default);

            // We set the MSBuildExtensionsPath variables here because we don't want to make them official
            // reserved properties; we need the ability for people to override our default in their
            // environment or as a global property.

#if !FEATURE_INSTALLED_MSBUILD
            string extensionsPath = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;
            string extensionsPath32 = extensionsPath;
#else
            // "MSBuildExtensionsPath32". This points to whatever the value of "Program Files (x86)" environment variable is;
            // but on a 32 bit box this isn't set, and we should use "Program Files" instead.
            string programFiles32 = FrameworkLocationHelper.programFiles32;
            string extensionsPath32 = NativeMethodsShared.IsWindows
                                          ? Path.Combine(programFiles32, ReservedPropertyNames.extensionsPathSuffix)
                                          : programFiles32;
#endif
            envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.extensionsPath32, extensionsPath32, true));

#if !FEATURE_INSTALLED_MSBUILD
            string extensionsPath64 = extensionsPath;
            envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.extensionsPath64, extensionsPath64, true));
#else
            // "MSBuildExtensionsPath64". This points to whatever the value of "Program Files" environment variable is on a
            // 64-bit machine, and is empty on a 32-bit machine.
            if (FrameworkLocationHelper.programFiles64 != null)
            {
                // if ProgramFiles and ProgramFiles(x86) are the same, then this is a 32-bit box,
                // so we only want to set MSBuildExtensionsPath64 if they're not
                string extensionsPath64 = NativeMethodsShared.IsWindows
                                              ? Path.Combine(
                                                  FrameworkLocationHelper.programFiles64,
                                                  ReservedPropertyNames.extensionsPathSuffix)
                                              : FrameworkLocationHelper.programFiles64;
                envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.extensionsPath64, extensionsPath64, true));
            }
#endif

#if FEATURE_INSTALLED_MSBUILD
            // MSBuildExtensionsPath:  The way this used to work is that it would point to "Program Files\MSBuild" on both
            // 32-bit and 64-bit machines.  We have a switch to continue using that behavior; however the default is now for
            // MSBuildExtensionsPath to always point to the same location as MSBuildExtensionsPath32.

            bool useLegacyMSBuildExtensionsPathBehavior = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDLEGACYEXTENSIONSPATH"));

            string programFiles = FrameworkLocationHelper.programFiles;
            string extensionsPath;
            if (useLegacyMSBuildExtensionsPathBehavior)
            {
                extensionsPath = Path.Combine(programFiles, ReservedPropertyNames.extensionsPathSuffix);
            }
            else
            {
                extensionsPath = extensionsPath32;
            }
#endif

            envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.extensionsPath, extensionsPath, true));

            // Windows XP and Windows Server 2003 don't define LocalAppData in their environment.
            // We'll set it here if the environment doesn't have it so projects can reliably
            // depend on $(LocalAppData).
            string localAppData = String.Empty;
            ProjectPropertyInstance localAppDataProp = envPropertiesHashSet.Get(ReservedPropertyNames.localAppData);
            if (localAppDataProp != null)
            {
                localAppData = localAppDataProp.EvaluatedValue;
            }

            if (String.IsNullOrEmpty(localAppData))
            {
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (String.IsNullOrEmpty(localAppData))
            {
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            if (String.IsNullOrEmpty(localAppData))
            {
                localAppData = BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory;
            }


            envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.localAppData, localAppData));

            // Add MSBuildUserExtensionsPath at $(LocalAppData)\Microsoft\MSBuild
            string userExtensionsPath = Path.Combine(localAppData, ReservedPropertyNames.userExtensionsPathSuffix);
            envPropertiesHashSet.Add(ProjectPropertyInstance.Create(ReservedPropertyNames.userExtensionsPath, userExtensionsPath));

            foreach (KeyValuePair<string, string> environmentVariable in environmentVariablesBag)
            {
                // We're going to just skip environment variables that contain names
                // with characters we can't handle. There's no logger registered yet
                // when this method is called, so we can't really log anything.
                string environmentVariableName = environmentVariable.Key;

                if (XmlUtilities.IsValidElementName(environmentVariableName) &&
                    !XMakeElements.ReservedItemNames.Contains(environmentVariableName) &&
                    !ReservedPropertyNames.IsReservedProperty(environmentVariableName))
                {
                    ProjectPropertyInstance environmentProperty = ProjectPropertyInstance.Create(environmentVariableName, environmentVariable.Value);

                    envPropertiesHashSet.Add(environmentProperty);
                }
                else
                {
                    // The name was invalid, so we just didn't add the environment variable.
                    // That's fine, continue for the next one.
                }
            }

            if (makeReadOnly)
            {
                envPropertiesHashSet.MakeReadOnly();
            }

            var environmentProperties = new PropertyDictionary<ProjectPropertyInstance>(envPropertiesHashSet);
            return environmentProperties;
        }

#if !NET
        /// <summary>
        /// Ensures that the capacity of this list is at least the specified <paramref name="capacity"/>.
        /// If the current capacity of the list is less than specified <paramref name="capacity"/>,
        /// the capacity is increased by continuously twice current capacity until it is at least the specified <paramref name="capacity"/>.
        /// </summary>
        /// <typeparam name="T">The type contained in the list.</typeparam>
        /// <param name="list">The list to adjust the capacity of.</param>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        /// <returns>The new capacity of this list.</returns>
        public static int EnsureCapacity<T>(this List<T> list, int capacity)
        {
            const int DefaultCapacity = 4;
            const int MaxArrayLength = 0X7FFFFFC7;

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (capacity > list.Capacity)
            {
                // Implementation copied and slightly modified from List's internal implementation.
                int newCapacity = list.Count == 0 ? DefaultCapacity : 2 * list.Capacity;

                // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
                // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
                if ((uint)newCapacity > MaxArrayLength)
                {
                    newCapacity = MaxArrayLength;
                }

                // If the computed capacity is still less than specified, set to the original argument.
                // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
                if (newCapacity < capacity)
                {
                    newCapacity = capacity;
                }

                list.Capacity = newCapacity;
            }

            return list.Capacity;
        }
#endif

        /// <summary>
        /// Extension to IEnumerable to get the count if it
        /// can be quickly gotten, otherwise 0.
        /// </summary>
        public static int FastCountOrZero(this IEnumerable enumerable)
        {
            ICollection collection = enumerable as ICollection;

            return collection?.Count ?? 0;
        }

        /// <summary>
        /// Extension to IEnumerable of KVP of string, something to just return the somethings.
        /// </summary>
        public static IEnumerable<T> Values<T>(this IEnumerable<KeyValuePair<string, T>> source) where T : class, IKeyed
        {
            foreach (var entry in source)
            {
                yield return entry.Value;
            }
        }

        /// <summary>
        /// Iterates through the nongeneric enumeration and provides generic strong-typed enumeration of properties.
        /// </summary>
        public static IEnumerable<PropertyData> EnumerateProperties(IEnumerable properties)
        {
            if (properties == null)
            {
                return [];
            }

            if (properties is PropertyDictionary<ProjectPropertyInstance> propertyInstanceDictionary)
            {
                return propertyInstanceDictionary.Enumerate();
            }
            else if (properties is PropertyDictionary<ProjectProperty> propertyDictionary)
            {
                return propertyDictionary.Enumerate();
            }
            else
            {
                return CastOneByOne(properties);
            }

            IEnumerable<PropertyData> CastOneByOne(IEnumerable props)
            {
                foreach (var item in props)
                {
                    if (item is IProperty property && !string.IsNullOrEmpty(property.Name))
                    {
                        yield return new(property.Name, property.EvaluatedValue ?? string.Empty);
                    }
                    else if (item is DictionaryEntry dictionaryEntry && dictionaryEntry.Key is string key && !string.IsNullOrEmpty(key))
                    {
                        yield return new(key, dictionaryEntry.Value as string ?? string.Empty);
                    }
                    else if (item is KeyValuePair<string, string> kvp)
                    {
                        yield return new(kvp.Key, kvp.Value);
                    }
                    else if (item is KeyValuePair<string, TimeSpan> keyTimeSpanValue)
                    {
                        yield return new(keyTimeSpanValue.Key, keyTimeSpanValue.Value.Ticks.ToString());
                    }
                    else
                    {
                        if (item == null)
                        {
                            Debug.Fail($"In {nameof(EnumerateProperties)}(): Unexpected: property is null");
                        }
                        else
                        {
                            Debug.Fail($"In {nameof(EnumerateProperties)}(): Unexpected property {item} of type {item?.GetType().ToString()}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Iterates through the nongeneric enumeration and provides generic strong-typed callback to handle the properties.
        /// </summary>
        public static void EnumerateProperties<TArg>(IEnumerable properties, TArg arg, Action<TArg, KeyValuePair<string, string>> callback)
        {
            foreach (var tuple in EnumerateProperties(properties))
            {
                callback(arg, new KeyValuePair<string, string>(tuple.Name, tuple.Value));
            }
        }

        /// <summary>
        /// Enumerates the given nongeneric enumeration and tries to match or wrap appropriate item types.
        /// </summary>
        public static IEnumerable<ItemData> EnumerateItems(IEnumerable items)
        {
            // The actual type of the item data can be of types:
            //  * <see cref="ProjectItemInstance"/>
            //  * <see cref="ProjectItem"/>
            //  * <see cref="IItem"/>
            //  * <see cref="ITaskItem"/>
            //  * possibly others
            // That's why we here wrap with ItemAccessor if needed

            if (items == null)
            {
                return [];
            }

            if (items is ItemDictionary<ProjectItemInstance> projectItemInstanceDictionary)
            {
                return projectItemInstanceDictionary
                    .EnumerateItemsPerType()
                    .Select(t => t.itemValue.Select(itemValue => new ItemData(t.itemType, (IItemData)itemValue)))
                    .SelectMany(tpl => tpl);
            }
            else if (items is ItemDictionary<ProjectItem> projectItemDictionary)
            {
                return projectItemDictionary
                    .EnumerateItemsPerType()
                    .Select(t => t.itemValue.Select(itemValue => new ItemData(t.itemType, (IItemData)itemValue)))
                    .SelectMany(tpl => tpl);
            }
            else
            {
                return CastItemsOneByOne(items, null);
            }
        }

        /// <summary>
        /// Enumerates the given nongeneric enumeration and tries to match or wrap appropriate item types.
        /// Only items with matching type (case insensitive, MSBuild valid names only) will be returned.
        /// </summary>
        public static IEnumerable<ItemData> EnumerateItemsOfType(IEnumerable items, string typeName)
        {
            if (items == null)
            {
                return [];
            }

            if (items is ItemDictionary<ProjectItemInstance> projectItemInstanceDictionary)
            {
                return
                    projectItemInstanceDictionary[typeName]
                        .Select(i => new ItemData(i.ItemType, (IItemData)i));
            }
            else if (items is ItemDictionary<ProjectItem> projectItemDictionary)
            {
                return
                    projectItemDictionary[typeName]
                        .Select(i => new ItemData(i.ItemType, (IItemData)i));
            }
            else
            {
                return CastItemsOneByOne(items, [typeName]);
            }
        }

        /// <summary>
        /// Enumerates the given nongeneric enumeration and tries to match or wrap appropriate item types.
        /// Only items with matching type (case insensitive, MSBuild valid names only) will be returned.
        /// </summary>
        public static IEnumerable<ItemData> EnumerateItemsOfTypes(IEnumerable items, string[] typeNames)
        {
            if (items == null)
            {
                return [];
            }

            if (items is ItemDictionary<ProjectItemInstance> projectItemInstanceDictionary)
            {
                return typeNames.Select(typeName =>
                    projectItemInstanceDictionary[typeName]
                        .Select(i => new ItemData(i.ItemType, (IItemData)i)))
                        .SelectMany(j => j);
            }
            else if (items is ItemDictionary<ProjectItem> projectItemDictionary)
            {
                return typeNames.Select(typeName =>
                        projectItemDictionary[typeName]
                            .Select(i => new ItemData(i.ItemType, (IItemData)i)))
                    .SelectMany(j => j);
            }
            else
            {
                return CastItemsOneByOne(items, typeNames);
            }
        }

        /// <summary>
        /// Iterates through the nongeneric enumeration of items and provides generic strong-typed callback to handle the items.
        /// </summary>
        public static void EnumerateItems(IEnumerable items, Action<DictionaryEntry> callback)
        {
            foreach (var tuple in EnumerateItems(items))
            {
                callback(new DictionaryEntry(tuple.Type, tuple.Value));
            }
        }

        /// <summary>
        /// Enumerates the nongeneric items and attempts to cast them.
        /// </summary>
        /// <param name="items">Nongeneric list of items.</param>
        /// <param name="itemTypeNamesToFetch">If not null, only the items with matching type (case insensitive, MSBuild valid names only) will be returned.</param>
        /// <returns></returns>
        private static IEnumerable<ItemData> CastItemsOneByOne(IEnumerable items, string[] itemTypeNamesToFetch)
        {
            IEnumerator enumerator = items.GetEnumerator();
            if (enumerator is List<DictionaryEntry>.Enumerator listEnumerator)
            {
                while (listEnumerator.MoveNext())
                {
                    DictionaryEntry dictionaryEntry = listEnumerator.Current;
                    string itemType = dictionaryEntry.Key as string;
                    object itemValue = dictionaryEntry.Value;

                    // if itemTypeNameToFetch was not set - then return all items
                    if (itemValue != null && (itemTypeNamesToFetch == null || MatchesAnyItemTypeToFetch(itemTypeNamesToFetch, itemType)))
                    {
                        // The ProjectEvaluationFinishedEventArgs.Items are currently assigned only in Evaluator.Evaluate()
                        //  where the only types that can be assigned are ProjectItem or ProjectItemInstance
                        // However! NodePacketTranslator and BuildEventArgsReader might deserialize those as TaskItemData
                        //  (see xml comments of TaskItemData for details)
                        yield return new ItemData(itemType!, itemValue);
                    }
                }
            }
            else
            {
                while (enumerator.MoveNext())
                {
                    object item = enumerator.Current;
                    string itemType = default;
                    object itemValue = null;

                    if (item is IItem iitem)
                    {
                        itemType = iitem.Key;
                        itemValue = iitem;
                    }
                    else if (item is DictionaryEntry dictionaryEntry)
                    {
                        itemType = dictionaryEntry.Key as string;
                        itemValue = dictionaryEntry.Value;
                    }
                    else
                    {
                        if (item == null)
                        {
                            Debug.Fail($"In {nameof(EnumerateItems)}(): Unexpected: {nameof(item)} is null");
                        }
                        else
                        {
                            Debug.Fail($"In {nameof(EnumerateItems)}(): Unexpected {nameof(item)} {item} of type {item?.GetType().ToString()}");
                        }
                    }

                    // if itemTypeNameToFetch was not set - then return all items
                    if (itemValue != null && (itemTypeNamesToFetch == null || MatchesAnyItemTypeToFetch(itemTypeNamesToFetch, itemType)))
                    {
                        // The ProjectEvaluationFinishedEventArgs.Items are currently assigned only in Evaluator.Evaluate()
                        //  where the only types that can be assigned are ProjectItem or ProjectItemInstance
                        // However! NodePacketTranslator and BuildEventArgsReader might deserialize those as TaskItemData
                        //  (see xml comments of TaskItemData for details)
                        yield return new ItemData(itemType!, itemValue);
                    }
                }
            }

            // PERF: This replaces a previous call to Any() that was causing an allocation due to a closure.
            static bool MatchesAnyItemTypeToFetch(string[] itemTypeNamesToFetch, string itemType)
            {
                foreach (string tp in itemTypeNamesToFetch)
                {
                    if (MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, tp))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
