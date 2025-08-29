﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A task that sets values as specified by XPath Query
    /// into a XML file.
    /// </summary>
    public class XmlPoke : TaskExtension
    {
        #region Members
        /// <summary>
        /// The XML input as file paths.
        /// </summary>
        private ITaskItem _xmlInputPath;

        /// <summary>
        /// The XPath Query.
        /// </summary>
        private string _query;

        #endregion

        #region Properties
        /// <summary>
        /// The XML input as file path.
        /// </summary>
        public ITaskItem XmlInputPath
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_xmlInputPath, nameof(XmlInputPath));
                return _xmlInputPath;
            }

            set => _xmlInputPath = value;
        }

        /// <summary>
        /// The XPath Query.
        /// </summary>
        public string Query
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_query, nameof(Query));
                return _query;
            }

            set => _query = value;
        }

        /// <summary>
        /// The value to be inserted into the specified location.
        /// </summary>        
        public ITaskItem Value { get; set; }

        /// <summary>
        /// The namespaces for XPath query's prefixes.
        /// </summary>
        public string Namespaces { get; set; }

        #endregion

        /// <summary>
        /// Executes the XMLPoke task.
        /// </summary>
        /// <returns>true if transformation succeeds.</returns>
        public override bool Execute()
        {
            ErrorUtilities.VerifyThrowArgumentNull(_query, "Query");
            ErrorUtilities.VerifyThrowArgumentNull(_xmlInputPath, "XmlInputPath");
            if (Value == null)
            {
                // When Value is null, it means Value is not set or empty. Here we treat them all as empty.
                Value = new TaskItem(String.Empty);
            }

            // Load the XPath Document
            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                using (FileStream fs = new FileStream(_xmlInputPath.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    XmlReaderSettings xrs = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                    using (XmlReader sr = XmlReader.Create(fs, xrs))
                    {
                        xmlDoc.Load(sr);
                    }
                }
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XmlPeekPoke.InputFileError", _xmlInputPath.ItemSpec, e.Message);
                return false;
            }

            XPathNavigator nav = xmlDoc.CreateNavigator();
            XPathExpression expr;

            try
            {
                // Create the expression from query
                expr = nav.Compile(_query);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XmlPeekPoke.XPathError", _query, e.Message);
                return false;
            }

            // Create the namespace manager and parse the input.
            var xmlNamespaceManager = new XmlNamespaceManager(nav.NameTable);

            // Arguments parameters
            try
            {
                LoadNamespaces(ref xmlNamespaceManager, Namespaces);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("XmlPoke.NamespacesError", e.Message);
                return false;
            }

            try
            {
                expr.SetContext(xmlNamespaceManager);
            }
            catch (XPathException e)
            {
                Log.LogErrorWithCodeFromResources("XmlPoke.XPathContextError", e.Message);
                return false;
            }

            XPathNodeIterator iter = nav.Select(expr);
            int count = 0;

            while (iter.MoveNext())
            {
                try
                {
                    count++;
                    iter.Current.InnerXml = Value.ItemSpec;
                    Log.LogMessageFromResources(MessageImportance.Low, "XmlPoke.Replaced", iter.Current.Name, Value.ItemSpec);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    Log.LogErrorWithCodeFromResources("XmlPoke.PokeError", Value.ItemSpec, e.Message);
                    return false;
                }
            }

            Log.LogMessageFromResources(MessageImportance.Normal, "XmlPoke.Count", count);

            if (count > 0)
            {
#if RUNTIME_TYPE_NETCORE
                using (Stream stream = File.Create(_xmlInputPath.ItemSpec))
                {
                    xmlDoc.Save(stream);
                }
#else
                xmlDoc.Save(_xmlInputPath.ItemSpec);
#endif
            }

            return true;
        }

        /// <summary>
        /// Loads the namespaces specified at Namespaces parameter to XmlNSManager.
        /// </summary>
        /// <param name="namespaceManager">The namespace manager to load namespaces to.</param>
        /// <param name="namepaces">The namespaces as XML snippet.</param>
        private static void LoadNamespaces(ref XmlNamespaceManager namespaceManager, string namepaces)
        {
            var doc = new XmlDocument();
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (XmlReader reader = XmlReader.Create(new StringReader("<Namespaces>" + namepaces + "</Namespaces>"), settings))
                {
                    doc.Load(reader);
                }
            }
            catch (XmlException xe)
            {
                throw new ArgumentException(ResourceUtilities.GetResourceString("XmlPoke.NamespacesParameterNotWellFormed"), xe);
            }

            XmlNodeList xnl = doc.SelectNodes("/Namespaces/*[local-name() = 'Namespace']");

            for (int i = 0; i < xnl?.Count; i++)
            {
                XmlNode xn = xnl[i];

                const string prefixAttr = "Prefix";
                XmlAttribute prefix = xn.Attributes?[prefixAttr];
                if (prefix == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("XmlPoke.NamespacesParameterNoAttribute", prefixAttr));
                }

                const string uriAttr = "Uri";
                XmlAttribute uri = xn.Attributes[uriAttr];
                if (uri == null)
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("XmlPoke.NamespacesParameterNoAttribute", uriAttr));
                }

                namespaceManager.AddNamespace(prefix.Value, uri.Value);
            }
        }
    }
}
