// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Transforms Xml with Xsl.</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that transforms a XML input with an XSLT or Compiled XSLT
    /// and outputs to screen or specified file.
    /// </summary>
    public class XslTransformation : TaskExtension
    {
        #region Members
        /// <summary>
        /// The XML input as file paths.
        /// </summary>
        private ITaskItem[] _xmlInputPaths;

        /// <summary>
        /// The XML input as string.
        /// </summary>
        private string _xmlString;

        /// <summary>
        /// The XSLT input as file path.
        /// </summary>
        private ITaskItem _xsltFile;

        /// <summary>
        /// The XSLT input as string.
        /// </summary>
        private string _xsltString;

        /// <summary>
        /// The XSLT input as compiled dll.
        /// </summary>
        private ITaskItem _xsltCompiledDll;

        /// <summary>
        /// The output files.
        /// </summary>
        private ITaskItem[] _outputPaths;

        /// <summary>
        /// The parameters to XSLT Input document.
        /// </summary>
        private string _parameters;

        /// <summary>
        /// Determines whether or not to use trusted settings when loading the Xslt file
        /// </summary>
        private bool _useTrustedSettings = false;
        #endregion

        #region Properties
        /// <summary>
        /// The XML input as file path.
        /// </summary>
        public ITaskItem[] XmlInputPaths
        {
            get
            {
                return _xmlInputPaths;
            }

            set
            {
                _xmlInputPaths = value;
            }
        }

        /// <summary>
        /// The XML input as string.
        /// </summary>
        public string XmlContent
        {
            get
            {
                return _xmlString;
            }

            set
            {
                _xmlString = value;
            }
        }

        /// <summary>
        /// The XSLT input as file path.
        /// </summary>
        public ITaskItem XslInputPath
        {
            get
            {
                return _xsltFile;
            }

            set
            {
                _xsltFile = value;
            }
        }

        /// <summary>
        /// The XSLT input as string.
        /// </summary>
        public string XslContent
        {
            get
            {
                return _xsltString;
            }

            set
            {
                _xsltString = value;
            }
        }

        /// <summary>
        /// The XSLT input as compiled dll.
        /// </summary>
        public ITaskItem XslCompiledDllPath
        {
            get
            {
                return _xsltCompiledDll;
            }

            set
            {
                _xsltCompiledDll = value;
            }
        }

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

            set
            {
                _outputPaths = value;
            }
        }

        /// <summary>
        /// The parameters to XSLT Input document.
        /// </summary>
        public string Parameters
        {
            get
            {
                return _parameters;
            }

            set
            {
                _parameters = value;
            }
        }

        /// <summary>
        /// Determines whether or not to use trusted settings. Default is false.
        /// </summary>
        public bool UseTrustedSettings
        {
            get
            {
                return _useTrustedSettings;
            }

            set
            {
                _useTrustedSettings = value;
            }
        }
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
                xmlinput = new XmlInput(_xmlInputPaths, _xmlString);
                xsltinput = new XsltInput(_xsltFile, _xsltString, _xsltCompiledDll, Log);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("XslTransform.ArgumentError", e.Message);
                return false;
            }

            // Check if OutputPath has same number of parameters as xmlInputPaths.
            if (_xmlInputPaths != null && _xmlInputPaths.Length != _outputPaths.Length)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", _outputPaths.Length, _xmlInputPaths.Length, "XmlContent", "XmlInputPaths");
                return false;
            }

            // Check if OutputPath has 1 parameter if xmlString is specified.
            if (_xmlString != null && _outputPaths.Length != 1)
            {
                Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", _outputPaths.Length, 1, "XmlContent", "OutputPaths");
                return false;
            }

            XsltArgumentList arguments;

            // Arguments parameters
            try
            {
                arguments = ProcessXsltArguments(_parameters);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("XslTransform.XsltArgumentsError", e.Message);
                return false;
            }

            XslCompiledTransform xslct;

            // Load the XSLT
            try
            {
                xslct = xsltinput.LoadXslt(UseTrustedSettings);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("XslTransform.XsltLoadError", e.Message);
                return false;
            }

            // Do the transformation.
            try
            {
                for (int i = 0; i < xmlinput.Count; i++)
                {
                    using (XmlWriter xmlWriter = XmlWriter.Create(_outputPaths[i].ItemSpec, xslct.OutputSettings))
                    {
                        using (XmlReader xr = xmlinput.CreateReader(i))
                        {
                            xslct.Transform(xr, arguments, xmlWriter);
                        }

                        xmlWriter.Close();
                    }
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                Log.LogErrorWithCodeFromResources("XslTransform.TransformError", e.Message);
                return false;
            }

            // Copy Metadata
            if (xmlinput.XmlMode == XmlInput.XmlModes.XmlFile)
            {
                for (int i = 0; i < _xmlInputPaths.Length; i++)
                {
                    _xmlInputPaths[i].CopyMetadataTo(_outputPaths[i]);
                }
            }

            return true;
        }

        /// <summary>
        /// Takes the raw XML and loads XsltArgumentList
        /// </summary>
        /// <param name="xsltParametersXml">The raw XML that holds each parameter as <Parameter Name="" Value="" Namespace="" /> </param>
        /// <returns>XsltArgumentList</returns>
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
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;
                XmlReader reader = XmlReader.Create(new StringReader("<XsltParameters>" + xsltParametersXml + "</XsltParameters>"), settings);
                doc.Load(reader);
            }
            catch (XmlException xe)
            {
                throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XsltParameterNotWellFormed"), xe);
            }

            XmlNodeList xnl = doc.SelectNodes("/XsltParameters/*[local-name() = 'Parameter']");

            for (int i = 0; i < xnl.Count; i++)
            {
                XmlNode xn = xnl[i];

                if (xn.Attributes["Name"] == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XsltParameterNoAttribute", "Name"));
                }

                if (xn.Attributes["Value"] == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XsltParameterNoAttribute", "Value"));
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
            /// What XML input type are we at.
            /// </summary>
            private XmlModes _xmlMode;

            /// <summary>
            /// This either contains the raw Xml or the path to Xml file.
            /// </summary>
            private string[] _data;

            /// <summary>
            /// Constructor.
            /// Only one parameter should be non null or will throw ArgumentException.
            /// </summary>
            /// <param name="xmlFile">The path to XML file or null.</param>
            /// <param name="xml">The raw XML.</param>
            public XmlInput(ITaskItem[] xmlFile, string xml)
            {
                if (xmlFile != null && xml != null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XmlInput.TooMany"));
                }
                else if (xmlFile == null && xml == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XmlInput.TooFew"));
                }

                if (xmlFile != null)
                {
                    _xmlMode = XmlModes.XmlFile;
                    _data = new string[xmlFile.Length];
                    for (int i = 0; i < xmlFile.Length; i++)
                    {
                        _data[i] = xmlFile[i].ItemSpec;
                    }
                }
                else
                {
                    _xmlMode = XmlModes.Xml;
                    _data = new string[] { xml };
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
            public int Count
            {
                get
                {
                    return _data.Length;
                }
            }

            /// <summary>
            /// Returns the current mode of the XmlInput
            /// </summary>
            public XmlModes XmlMode
            {
                get
                {
                    return _xmlMode;
                }
            }

            /// <summary>
            /// Creates correct reader based on the input type.
            /// </summary>
            /// <returns>The XmlReader object</returns>
            public XmlReader CreateReader(int itemPos)
            {
                if (_xmlMode == XmlModes.XmlFile)
                {
                    return XmlReader.Create(_data[itemPos]);
                }
                else // xmlModes.Xml 
                {
                    return XmlReader.Create(new StringReader(_data[itemPos]));
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
            private XslModes _xslMode;

            /// <summary>
            /// Contains the raw XSLT 
            /// or the path to XSLT file
            /// or the path to compiled XSLT dll.
            /// </summary>
            private string _data;

            /// <summary>
            /// Tool for logging build messages, warnings, and errors
            /// </summary>
            private TaskLoggingHelper _log;

            /// <summary>
            /// Constructer.
            /// Only one parameter should be non null or will throw ArgumentException.
            /// </summary>
            /// <param name="xsltFile">The path to XSLT file or null.</param>
            /// <param name="xslt">The raw to XSLT or null.</param>
            /// <param name="xsltCompiledDll">The path to compiled XSLT file or null.</param>
            public XsltInput(ITaskItem xsltFile, string xslt, ITaskItem xsltCompiledDll, TaskLoggingHelper logTool)
            {
                _log = logTool;
                if ((xsltFile != null && xslt != null) ||
                    (xsltFile != null && xsltCompiledDll != null) ||
                    (xslt != null && xsltCompiledDll != null))
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XsltInput.TooMany"));
                }
                else if (xsltFile == null && xslt == null && xsltCompiledDll == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.XsltInput.TooFew"));
                }

                if (xsltFile != null)
                {
                    _xslMode = XslModes.XsltFile;
                    _data = xsltFile.ItemSpec;
                }
                else if (xslt != null)
                {
                    _xslMode = XslModes.Xslt;
                    _data = xslt;
                }
                else
                {
                    _xslMode = XslModes.XsltCompiledDll;
                    _data = xsltCompiledDll.ItemSpec;
                }
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
                        xslct.Load(XmlReader.Create(new StringReader(_data)), settings, new XmlUrlResolver());
                        break;
                    case XslModes.XsltFile:
                        if (useTrustedSettings)
                        {
                            settings = XsltSettings.TrustedXslt;
                        }
                        else
                        {
                            _log.LogMessageFromResources(MessageImportance.Low, "XslTransform.UseTrustedSettings", _data);
                        }

                        xslct.Load(new XPathDocument(XmlReader.Create(_data)), settings, new XmlUrlResolver());
                        break;
#if !MONO
                    case XslModes.XsltCompiledDll:
                        // We accept type in format: assembly_name[;type_name]. type_name may be omitted if assembly has just one type defined
                        string dll = _data;
                        string[] pair = dll.Split(';');
                        string assemblyPath = pair[0];
                        string typeName = (pair.Length == 2) ? pair[1] : null;

                        Type t = FindType(assemblyPath, typeName);
                        xslct.Load(t);
                        break;
#endif
                    default:
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                        break;
                }

                return xslct;
            }

            /// <summary>
            /// Find the type from an assembly and loads it.
            /// </summary>
            /// <param name="assemblyPath">The path to assembly.</param>
            /// <param name="typeName">The type name.</param>
            /// <returns>Found type.</returns>
            private static Type FindType(string assemblyPath, string typeName)
            {
                AssemblyName assemblyName = new AssemblyName();
                assemblyName.CodeBase = assemblyPath;
                Assembly loadedAssembly = Assembly.Load(assemblyName);
                if (typeName != null)
                {
                    return loadedAssembly.GetType(typeName);
                }
                else
                {
                    List<Type> types = new List<Type>();
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

                    throw new ArgumentException(ResourceUtilities.FormatResourceString("XslTransform.MustSpecifyType", assemblyPath));
                }
            }
        }
        #endregion
    }
}
