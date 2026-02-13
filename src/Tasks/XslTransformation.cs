// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#if FEATURE_COMPILED_XSL
using System.Collections.Generic;
using System.Reflection;
#endif

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that transforms a XML input with an XSLT or Compiled XSLT
    /// and outputs to screen or specified file.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class XslTransformation : TaskExtension, IMultiThreadableTask
    {
        #region Members

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; }

        /// <summary>
        /// The output files.
        /// </summary>
        private ITaskItem[] _outputPaths;

        #endregion

        #region Properties
        /// <summary>
        /// The XML input as file path.
        /// </summary>
        public ITaskItem[] XmlInputPaths { get; set; }

        /// <summary>
        /// The XML input as string.
        /// </summary>
        public string XmlContent { get; set; }

        /// <summary>
        /// The XSLT input as file path.
        /// </summary>
        public ITaskItem XslInputPath { get; set; }

        /// <summary>
        /// The XSLT input as string.
        /// </summary>
        public string XslContent { get; set; }

        /// <summary>
        /// Flag to preserve whitespaces in the XSLT file.
        /// </summary>
        public bool PreserveWhitespace { get; set; }

        /// <summary>
        /// The XSLT input as compiled dll.
        /// </summary>
        public ITaskItem XslCompiledDllPath { get; set; }

        /// <summary>
        /// The output file.
        /// </summary>
        [Required]
        public ITaskItem[] OutputPaths
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_outputPaths, "OutputPath");
                return _outputPaths;
            }

            set => _outputPaths = value;
        }

        /// <summary>
        /// The parameters to XSLT Input document.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Determines whether or not to use trusted settings. Default is false.
        /// </summary>
        public bool UseTrustedSettings { get; set; }

        #endregion

        /// <summary>
        /// Executes the XslTransform task.
        /// </summary>
        /// <returns>true if transformation succeeds.</returns>
        public override bool Execute()
        {
            XmlInput xmlinput;
            XsltInput xsltinput;
            ErrorUtilities.VerifyThrowArgumentNull(_outputPaths, "OutputPath");

            // Load XmlInput, XsltInput parameters
            try
            {
                AbsolutePath?[] absoluteXmlInputPaths = XmlInputPaths != null
                    ? Array.ConvertAll(XmlInputPaths, item => item.ItemSpec != null ? TaskEnvironment.GetAbsolutePath(item.ItemSpec) : (AbsolutePath?)null)
                    : null;
                xmlinput = new XmlInput(absoluteXmlInputPaths, XmlContent);

                AbsolutePath? absoluteXslInputPath = XslInputPath?.ItemSpec != null ? TaskEnvironment.GetAbsolutePath(XslInputPath.ItemSpec) : null;
                xsltinput = new XsltInput(absoluteXslInputPath, XslContent, XslCompiledDllPath?.ItemSpec, TaskEnvironment, Log, PreserveWhitespace);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XslTransform.ArgumentError", e.Message);
                return false;
            }

            // Check if OutputPath has same number of parameters as xmlInputPaths.
            if (XmlInputPaths != null && XmlInputPaths.Length != _outputPaths.Length)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", _outputPaths.Length, XmlInputPaths.Length, "OutputPaths", "XmlInputPaths");
                return false;
            }

            // Check if OutputPath has 1 parameter if xmlString is specified.
            if (XmlContent != null && _outputPaths.Length != 1)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", _outputPaths.Length, 1, "OutputPaths", "XmlContent");
                return false;
            }

            XsltArgumentList arguments;

            // Arguments parameters
            try
            {
                arguments = ProcessXsltArguments(Parameters);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XslTransform.XsltArgumentsError", e.Message);
                return false;
            }

            XslCompiledTransform xslct;

            // Load the XSLT
            try
            {
                xslct = xsltinput.LoadXslt(UseTrustedSettings);
            }
            catch (PlatformNotSupportedException)
            {
                Log.LogErrorWithCodeFromResources("XslTransform.PrecompiledXsltError");
                return false;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XslTransform.XsltLoadError", e.Message);
                return false;
            }

            // Do the transformation.
            try
            {
                if (UseTrustedSettings)
                {
                    Log.LogMessageFromResources(MessageImportance.High, "XslTransform.SecuritySettingsViaUseTrustedSettings");
                }

                for (int i = 0; i < xmlinput.Count; i++)
                {
                    using (XmlWriter xmlWriter = XmlWriter.Create(TaskEnvironment.GetAbsolutePath(_outputPaths[i].ItemSpec), xslct.OutputSettings))
                    {
                        using (XmlReader xr = xmlinput.CreateReader(i))
                        {
                            xslct.Transform(xr, arguments, xmlWriter, new XmlUrlResolver());
                        }

                        xmlWriter.Close();
                    }
                }
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeAndExceptionFromResources(e, true, true, "XslTransform.TransformError", [e.Message]);
                return false;
            }

            // Copy Metadata
            if (xmlinput.XmlMode == XmlInput.XmlModes.XmlFile)
            {
                for (int i = 0; i < XmlInputPaths.Length; i++)
                {
                    XmlInputPaths[i].CopyMetadataTo(_outputPaths[i]);
                }
            }

            return true;
        }

        /// <summary>
        /// Takes the raw XML and loads XsltArgumentList
        /// </summary>
        /// <param name="xsltParametersXml">The raw XML that holds each parameter as <Parameter Name="" Value="" Namespace="" /> </param>
        /// <returns>XsltArgumentList.</returns>
        private static XsltArgumentList ProcessXsltArguments(string xsltParametersXml)
        {
            XsltArgumentList arguments = new XsltArgumentList();
            if (xsltParametersXml == null)
            {
                return arguments;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader reader = XmlReader.Create(new StringReader("<XsltParameters>" + xsltParametersXml + "</XsltParameters>"), settings))
                {
                    doc.Load(reader);
                }
            }
            catch (XmlException xe)
            {
                throw new ArgumentException(ResourceUtilities.GetResourceString("XslTransform.XsltParameterNotWellFormed"), xe);
            }

            XmlNodeList xnl = doc.SelectNodes("/XsltParameters/*[local-name() = 'Parameter']");

            for (int i = 0; i < xnl.Count; i++)
            {
                XmlNode xn = xnl[i];

                if (xn.Attributes["Name"] == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("XslTransform.XsltParameterNoAttribute", "Name"));
                }

                if (xn.Attributes["Value"] == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("XslTransform.XsltParameterNoAttribute", "Value"));
                }

                string ns = String.Empty;
                if (xn.Attributes["Namespace"] != null)
                {
                    ns = xn.Attributes["Namespace"].Value;
                }

                arguments.AddParam(xn.Attributes["Name"].Value, ns, xn.Attributes["Value"].Value);
            }

            return arguments;
        }

        #region Supporting classes for input
        /// <summary>
        /// This class prepares XML input from XmlFile and Xml parameters
        /// </summary>
        internal class XmlInput
        {
            /// <summary>
            /// This contains the absolute paths to Xml files when in XmlFile mode.
            /// </summary>
            private readonly AbsolutePath?[] _filePaths;

            /// <summary>
            /// This contains the raw Xml content when in Xml mode.
            /// </summary>
            private readonly string _xmlContent;

            /// <summary>
            /// Constructor.
            /// Only one parameter should be non null or will throw ArgumentException.
            /// </summary>
            /// <param name="xmlFilePaths">The absolute paths to XML files or null.</param>
            /// <param name="xml">The raw XML.</param>
            public XmlInput(AbsolutePath?[] xmlFilePaths, string xml)
            {
                if (xmlFilePaths != null && xml != null)
                {
                    throw new ArgumentException(ResourceUtilities.GetResourceString("XslTransform.XmlInput.TooMany"));
                }
                else if (xmlFilePaths == null && xml == null)
                {
                    throw new ArgumentException(ResourceUtilities.GetResourceString("XslTransform.XmlInput.TooFew"));
                }

                if (xmlFilePaths != null)
                {
                    XmlMode = XmlModes.XmlFile;
                    _filePaths = xmlFilePaths;
                }
                else
                {
                    XmlMode = XmlModes.Xml;
                    _xmlContent = xml;
                }
            }

            /// <summary>
            /// Possible accepted types of XML input.
            /// </summary>
            public enum XmlModes
            {
                /// <summary>
                /// If the mode is a XML file.
                /// </summary>
                XmlFile,

                /// <summary>
                /// If the mode is a raw XML.
                /// </summary>
                Xml
            }

            /// <summary>
            /// Returns the count of Xml Inputs
            /// </summary>
            public int Count => XmlMode == XmlModes.XmlFile ? _filePaths.Length : 1;

            /// <summary>
            /// Returns the current mode of the XmlInput
            /// </summary>
            public XmlModes XmlMode { get; }

            /// <summary>
            /// Creates correct reader based on the input type.
            /// </summary>
            /// <returns>The XmlReader object</returns>
            public XmlReader CreateReader(int itemPos)
            {
                if (XmlMode == XmlModes.XmlFile)
                {
                    return XmlReader.Create(new StreamReader(_filePaths[itemPos]), new XmlReaderSettings { CloseInput = true }, _filePaths[itemPos]);
                }
                else // xmlModes.Xml
                {
                    return XmlReader.Create(new StringReader(_xmlContent));
                }
            }
        }

        /// <summary>
        /// This class prepares XSLT input from XsltFile, Xslt and XsltCompiledDll parameters
        /// </summary>
        internal class XsltInput
        {
            /// <summary>
            /// What XSLT input type are we at.
            /// </summary>
            private readonly XslModes _xslMode;

            /// <summary>
            /// Contains the absolute path to XSLT file when in XsltFile mode.
            /// </summary>
            private readonly AbsolutePath? _filePath;

            /// <summary>
            /// Contains the raw XSLT content (Xslt mode) or compiled DLL specification (XsltCompiledDll mode).
            /// </summary>
            private readonly string _data;

#if FEATURE_COMPILED_XSL
            /// <summary>
            /// Task environment for resolving paths.
            /// </summary>
            private readonly TaskEnvironment _taskEnvironment;
#endif

            /// <summary>
            /// Flag to preserve whitespaces in the XSLT file.
            /// </summary>
            private bool _preserveWhitespace;

            /// <summary>
            /// Tool for logging build messages, warnings, and errors
            /// </summary>
            private readonly TaskLoggingHelper _log;

            /// <summary>
            /// Constructer.
            /// Only one parameter should be non null or will throw ArgumentException.
            /// </summary>
            /// <param name="xsltFilePath">The absolute path to XSLT file or null.</param>
            /// <param name="xslt">The raw to XSLT or null.</param>
            /// <param name="xsltCompiledDllSpec">The compiled XSLT DLL specification (assembly_path[;type_name]) or null.</param>
            /// <param name="taskEnvironment">Task environment for resolving paths.</param>
            /// <param name="logTool">Log helper.</param>
            /// <param name="preserveWhitespace">Flag for xslt whitespace option.</param>
            public XsltInput(AbsolutePath? xsltFilePath, string xslt, string xsltCompiledDllSpec, TaskEnvironment taskEnvironment, TaskLoggingHelper logTool, bool preserveWhitespace)
            {
                _log = logTool;
#if FEATURE_COMPILED_XSL
                _taskEnvironment = taskEnvironment;
#endif
                if ((xsltFilePath != null && xslt != null) ||
                    (xsltFilePath != null && xsltCompiledDllSpec != null) ||
                    (xslt != null && xsltCompiledDllSpec != null))
                {
                    throw new ArgumentException(ResourceUtilities.GetResourceString("XslTransform.XsltInput.TooMany"));
                }
                else if (xsltFilePath == null && xslt == null && xsltCompiledDllSpec == null)
                {
                    throw new ArgumentException(ResourceUtilities.GetResourceString("XslTransform.XsltInput.TooFew"));
                }

                if (xsltFilePath != null)
                {
                    _xslMode = XslModes.XsltFile;
                    _filePath = xsltFilePath;
                }
                else if (xslt != null)
                {
                    _xslMode = XslModes.Xslt;
                    _data = xslt;
                }
                else
                {
                    _xslMode = XslModes.XsltCompiledDll;
                    _data = xsltCompiledDllSpec;
                }

                _preserveWhitespace = preserveWhitespace;
            }

            /// <summary>
            /// Possible accepted types of XSLT input.
            /// </summary>
            public enum XslModes
            {
                /// <summary>
                /// If the mode is a XSLT file.
                /// </summary>
                XsltFile,

                /// <summary>
                /// If the mode is a raw XSLT.
                /// </summary>
                Xslt,

                /// <summary>
                /// If the mode is a compiled Xslt dll.
                /// </summary>
                XsltCompiledDll
            }

            /// <summary>
            /// Loads the XSLT to XslCompiledTransform. By default uses Default settings instead of trusted settings.
            /// </summary>
            /// <returns>A XslCompiledTransform object.</returns>
            public XslCompiledTransform LoadXslt()
            {
                return LoadXslt(false);
            }

            /// <summary>
            /// Loads the XSLT to XslCompiledTransform. By default uses Default settings instead of trusted settings.
            /// </summary>
            /// <param name="useTrustedSettings">Determines whether or not to use trusted settings.</param>
            /// <returns>A XslCompiledTransform object.</returns>
            public XslCompiledTransform LoadXslt(bool useTrustedSettings)
            {
                XslCompiledTransform xslct = new XslCompiledTransform();
                XsltSettings settings = XsltSettings.Default;

                switch (_xslMode)
                {
                    case XslModes.Xslt:
                        {
                            using var sr = new StringReader(_data);
                            using var xmlReader = XmlReader.Create(sr);
                            xslct.Load(xmlReader, settings, new XmlUrlResolver());
                            break;
                        }
                    case XslModes.XsltFile:
                        if (useTrustedSettings)
                        {
                            settings = XsltSettings.TrustedXslt;
                        }
                        else
                        {
                            _log.LogMessageFromResources(MessageImportance.Low, "XslTransform.UseTrustedSettings", _filePath.Value.OriginalValue);
                        }

                        using (XmlReader reader = XmlReader.Create(new StreamReader(_filePath.Value), new XmlReaderSettings { CloseInput = true }, _filePath.Value))
                        {
                            XmlSpace xmlSpaceOption = _preserveWhitespace ? XmlSpace.Preserve : XmlSpace.Default;
                            xslct.Load(new XPathDocument(reader, xmlSpaceOption), settings, new XmlUrlResolver());
                        }
                        break;
                    case XslModes.XsltCompiledDll:
#if FEATURE_COMPILED_XSL
                        // We accept type in format: assembly_name[;type_name]. type_name may be omitted if assembly has just one type defined
                        string[] pair = _data.Split(MSBuildConstants.SemicolonChar);
                        string assemblyPath = _taskEnvironment.GetAbsolutePath(pair[0]);
                        string typeName = (pair.Length == 2) ? pair[1] : null;

                        Type t = FindType(assemblyPath, typeName);
                        xslct.Load(t);
                        break;
#else
                        throw new PlatformNotSupportedException("Precompiled XSLTs are not supported in .NET Core");
#endif
                    default:
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                        break;
                }

                return xslct;
            }

#if FEATURE_COMPILED_XSL
            /// <summary>
            /// Find the type from an assembly and loads it.
            /// </summary>
            /// <param name="assemblyPath">The path to assembly.</param>
            /// <param name="typeName">The type name.</param>
            /// <returns>Found type.</returns>
            private static Type FindType(string assemblyPath, string typeName)
            {
                AssemblyName assemblyName = new AssemblyName { CodeBase = assemblyPath };
                Assembly loadedAssembly = Assembly.Load(assemblyName);
                if (typeName != null)
                {
                    return loadedAssembly.GetType(typeName);
                }
                else
                {
                    var types = new List<Type>();
                    foreach (Type type in loadedAssembly.GetTypes())
                    {
                        if (!type.Name.StartsWith("$", StringComparison.Ordinal))
                        {
                            types.Add(type);
                        }
                    }

                    if (types.Count == 1)
                    {
                        return types[0];
                    }

                    throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("XslTransform.MustSpecifyType", assemblyPath));
                }
            }
#endif
        }
        #endregion
    }
}
