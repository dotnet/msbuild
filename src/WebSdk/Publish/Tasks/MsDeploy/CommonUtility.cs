// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// CommonUtility.cs
///
/// Common utility function
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------
namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using Text = System.Text;
    using CultureInfo = System.Globalization.CultureInfo;
    using IO = System.IO;
    using RegularExpressions = System.Text.RegularExpressions;
    using Win32 = Microsoft.Win32;
    using Xml = System.Xml;
    using Framework = Microsoft.Build.Framework;
    using Utilities = Microsoft.Build.Utilities;
    using System.Diagnostics;
    using Generic = System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.NET.Sdk.Publish.Tasks.Properties;
    using System.IO;
    using System.Xml;

    internal enum PipelineMetadata
    {
        //<ItemDefinitionGroup>
        //  <FilesForPackagingFromProject>
        //    <DestinationRelativePath></DestinationRelativePath>
        //    <Exclude>False</Exclude>
        //    <FromTarget>Unknown</FromTarget>
        //    <Category>Run</Category>
        //  </FilesForPackagingFromProject>
        //</ItemDefinitionGroup>
        DestinationRelativePath,
        Exclude,
        FromTarget,
        Category,
    };



    internal enum ReplaceRuleMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployReplaceRules>
        //    <ObjectName></ObjectName>
        //    <ScopeAttributeName></ScopeAttributeName>
        //    <ScopeAttributeValue></ScopeAttributeValue>
        //    <TargetAttributeName></TargetAttributeName>
        //    <Match></Match>
        //    <Replace></Replace>
        //  </MsDeployReplaceRules>
        //</ItemDefinitionGroup>
        ObjectName,
        ScopeAttributeName,
        ScopeAttributeValue,
        TargetAttributeName,
        Match,
        Replace,
    };




    internal enum SkipRuleMetadata
    {
      //<ItemDefinitionGroup>
      //  <MsDeploySkipRules>
      //    <SkipAction></SkipAction>
      //    <ObjectName></ObjectName>
      //    <AbsolutePath></AbsolutePath>
      //    <XPath></XPath>
      //    <KeyAttribute></KeyAttribute>
      //  </MsDeploySkipRules>
      //</ItemDefinitionGroup>
        SkipAction,
        ObjectName,
        AbsolutePath,
        XPath,
        KeyAttribute,
    };

    internal enum DeclareParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployDeclareParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Description></Description>
        //    <DefaultValue></DefaultValue>
        //  </MsDeployDeclareParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Description,
        DefaultValue,
        Tags,
    };


    internal enum ExistingDeclareParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeployDeclareParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Description></Description>
        //    <DefaultValue></DefaultValue>
        //  </MsDeployDeclareParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
    };

    internal enum SimpleSyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySimpleSetParameters>
        //    <Value></Value>
        //  </MsDeploySimpleSetParameters>
        //</ItemDefinitionGroup>
        Value,
    }

    internal enum SqlCommandVariableMetaData
    {
        Value,
        IsDeclared,
        SourcePath,
        SourcePath_RegExExcaped,
        DestinationGroup
    }


    internal enum ExistingParameterValiationMetadata
    {
        Element,
        Kind,
        ValidationString,
    }
    internal enum SyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySetParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Value></Value>
        //  </MsDeploySetParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Value,
        Description,
        DefaultValue,
        Tags,
    };

    internal enum ExistingSyncParameterMetadata
    {
        //<ItemDefinitionGroup>
        //  <MsDeploySetParameters>
        //    <Kind></Kind>
        //    <Scope></Scope>
        //    <Match></Match>
        //    <Value></Value>
        //  </MsDeploySetParameters>
        //</ItemDefinitionGroup>
        Kind,
        Scope,
        Match,
        Value,
    };


    

    internal class ParameterInfo
    {
        public string Name;
        public string Value;
        public ParameterInfo(string parameterName, string parameterStringValue)
        {
            Name = parameterName;
            Value = parameterStringValue;
        }
    }

    internal class ProviderOption: ParameterInfo
    {
        public string FactoryName;
        public ProviderOption(string factorName, string parameterName, string parameterStringValue):
            base(parameterName, parameterStringValue)
        {
            FactoryName = factorName;
        }
    }


    internal class ParameterInfoWithEntry : ParameterInfo
    {
        //Kind,
        //Scope,
        //Match,
        //Value,
        //Description,
        //DefaultValue,
        public string Kind;
        public string Scope;
        public string Match;
        public string Description;
        public string DefaultValue;
        public string Tags;
        public string Element;
        public string ValidationString;

        public ParameterInfoWithEntry(string name, string value, string kind, string scope, string matchRegularExpression, string description, string defaultValue, string tags, string element, string validationString) :
            base(name, value)
        {
            Kind = kind;
            Scope = scope;
            Match = matchRegularExpression;
            Description = description;
            DefaultValue = defaultValue;
            Tags = tags;
            Element = element;
            ValidationString = validationString;

        }
    }


    internal static class Utility
    {
        static System.Collections.Generic.Dictionary<string, string> m_wellKnownNamesDict = null;
        static System.Collections.Generic.Dictionary<string, string> m_wellKnownNamesMsdeployDict = null;


        internal enum IISExpressMetadata
        {
            WebServerDirectory, WebServerManifest, WebServerAppHostConfigDirectory
        }
        
        public static bool IsInternalMsdeployWellKnownItemMetadata(string name)
        {
            IISExpressMetadata iisExpressMetadata;
            if (System.Enum.TryParse<IISExpressMetadata>(name, out iisExpressMetadata))
            {
                return true;
            }
            if (string.Compare(name, "Path", System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            return IsMSBuildWellKnownItemMetadata(name);
        }

        // Utility function to filter out known MSBuild Metatdata
        public static bool IsMSBuildWellKnownItemMetadata(string name)
        {
            if (m_wellKnownNamesDict == null)
            {
                string[] wellKnownNames = 
                {
                    "FullPath",
                    "RootDir",
                    "Filename",
                    "Extension",
                    "RelativeDir",
                    "Directory",
                    "RecursiveDir",
                    "Identity",
                    "ModifiedTime",
                    "CreatedTime",
                    "AccessedTime",
                    "OriginalItemSpec",
                    "DefiningProjectDirectory",
                    "DefiningProjectDirectoryNoRoot",
                    "DefiningProjectExtension",
                    "DefiningProjectFile",
                    "DefiningProjectFullPath",
                    "DefiningProjectName"
                };
                m_wellKnownNamesDict = new System.Collections.Generic.Dictionary<string, string>(wellKnownNames.GetLength(0), System.StringComparer.OrdinalIgnoreCase);
                
                foreach (string wellKnownName in wellKnownNames)
                {
                    m_wellKnownNamesDict.Add(wellKnownName, null);
                }
            }
            return m_wellKnownNamesDict.ContainsKey(name);
        }

        public static bool IsMsDeployWellKnownLocationInfo(string name)
        {
            if (m_wellKnownNamesMsdeployDict == null)
            {
                string[] wellKnownNames = 
                {
                   "computerName", 
                   "wmsvc", 
                   "userName", 
                   "password", 
                   "includeAcls", 
                   "encryptPassword", 
                   "authType", 
                   "prefetchPayload",
                };
                m_wellKnownNamesMsdeployDict = new System.Collections.Generic.Dictionary<string, string>(wellKnownNames.GetLength(0), System.StringComparer.OrdinalIgnoreCase);

                foreach (string wellKnownName in wellKnownNames)
                {
                    m_wellKnownNamesMsdeployDict.Add(wellKnownName, null);
                }
            }
            return m_wellKnownNamesMsdeployDict.ContainsKey(name);
        }

        static  Text.StringBuilder m_stringBuilder = null;

        /// <summary>
        /// commong utility for Clean share common builder
        /// </summary>
        private  static Text.StringBuilder StringBuilder
        {
            get
            {
                if (m_stringBuilder == null)
                {
                    m_stringBuilder = new System.Text.StringBuilder(1024);
                }
                return m_stringBuilder;
            }
        }

        /// <summary>
        /// This is the simple share clean build. Since this is an share instance
        /// make sure you don't call this on complex operation or it will be zero out unexpectly
        /// Use this you need to be simple function which doesn't call any functiont that use this property
        /// Sde dev10 bug 699893
        /// </summary>
        public static Text.StringBuilder CleanStringBuilder
        {
            get
            {
                Text.StringBuilder sb = StringBuilder;
                sb.Remove(0, sb.Length);
                return sb;
            }
        }

#if NET472
        /// <summary>
        /// Return the current machine's IIS version
        /// </summary>
        /// <returns></returns>
        public static uint GetInstalledMajorIisVersion()
        {
            uint iisMajorVersion = 0;
            using (Win32.RegistryKey registryKey = Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Inetstp"))
            {
                if (registryKey != null)
                {
                    iisMajorVersion = System.Convert.ToUInt16(registryKey.GetValue(@"MajorVersion", 0), CultureInfo.InvariantCulture);
                }
            }
            return iisMajorVersion;
        }
#endif

        /// <summary>
        /// verify it is in IIS6
        /// </summary>
        /// <param name="verFromTarget"></param>
        /// <returns></returns>
        public static bool IsIis6(string verFromTarget)
        {
            return (verFromTarget == "6");
        }

        /// <summary>
        /// Main version of IIS
        /// </summary>
        public enum IisMainVersion
        {
            NonIis = 0,
            Iis6 = 6,
            Iis7 = 7
        }

        /// <summary>
        /// Return true if MSDeploy is installed
        /// </summary>
        private static bool _isMSDeployInstalled = false;
        private static string _strErrorMessage = null;
        public static bool IsMSDeployInstalled {
            get {
                if (_isMSDeployInstalled) {
                    return true;
                }
                else if (_strErrorMessage != null) {
                    return false;
                }
                else {
                    try {
                        _isMSDeployInstalled = CheckMSDeploymentVersion();
                    }
                    catch (System.IO.FileNotFoundException ex) {
                        _strErrorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                            Resources.VSMSDEPLOY_MSDEPLOY32bit,
                            Resources.VSMSDEPLOY_MSDEPLOY64bit,
                            ex.Message);
                        _isMSDeployInstalled = false;
                    }

                    Debug.Assert(_isMSDeployInstalled || _strErrorMessage != null);
                    return _isMSDeployInstalled;
                }
            }
        }

        /// <summary>
        /// Return true if MSDeploy is installed, and report an error to task.Log if it's not
        /// </summary>
        public static bool CheckMSDeploymentVersion( Utilities.TaskLoggingHelper log , out string errorMessage) {
            errorMessage = null;
            if (!IsMSDeployInstalled) {
                errorMessage = _strErrorMessage;
                log.LogError(_strErrorMessage);
                return false;
            }
            else {
                return true;
            }
        }


        /// <summary>
        /// Utility function to save the given XML document in UTF8 and indented
        /// </summary>
        /// <param name="document"></param>
        public static void SaveDocument(Xml.XmlDocument document, string outputFileName, System.Text.Encoding encode)
        {
#if NET472
            Xml.XmlTextWriter textWriter = new Xml.XmlTextWriter(outputFileName, encode);
            textWriter.Formatting = System.Xml.Formatting.Indented;
            document.Save(textWriter);
            textWriter.Close();
#else
            using (FileStream fs = new FileStream(outputFileName, FileMode.OpenOrCreate))
            {
                using (StreamWriter writer = new StreamWriter(fs, encode))
                {
                    XmlDeclaration xmldecl;
                    xmldecl = document.CreateXmlDeclaration("1.0", null, null);
                    xmldecl.Encoding = "utf-8";

                    // Add the new node to the document.
                    XmlElement root = document.DocumentElement;
                    document.InsertBefore(xmldecl, root);

                    document.Save(writer);
                }
            }
#endif
        }

        /// <summary>
        /// Utility to check the MinimumVersion of Msdeploy
        /// </summary>
        static string s_strMinimumVersion;

        /// <summary>
        /// Helper function to determine installed MSDeploy version
        /// </summary>
        private static bool CheckMSDeploymentVersion() {
            // Find the MinimumVersionRequirement
            System.Version currentMinVersion;
            if (!string.IsNullOrEmpty(s_strMinimumVersion))
            {
                currentMinVersion = new System.Version(s_strMinimumVersion);
            }
            else
            {
                currentMinVersion = new System.Version(7, 1, 614); // current drop
                {
                    string strMinimumVersion = string.Empty;
#if NET472
                    using (Win32.RegistryKey registryKeyVs = Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\11.0\WebDeploy"))
                    {
                        if (registryKeyVs != null)
                        {
                            s_strMinimumVersion = registryKeyVs.GetValue(@"MinimumMsDeployVersion", string.Empty).ToString();
                        }
                        else
                        {
                            using (Win32.RegistryKey registryKeyVsLM = Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\11.0\WebDeploy"))
                            {
                                if (registryKeyVsLM != null)
                                {
                                    s_strMinimumVersion = registryKeyVsLM.GetValue(@"MinimumMsDeployVersion", string.Empty).ToString();
                                }
                            }
                        }
                    }
#endif
                    if (!string.IsNullOrEmpty(s_strMinimumVersion))
                    {
                        currentMinVersion = new System.Version(strMinimumVersion);
                    }
                    else
                    {
                        s_strMinimumVersion = currentMinVersion.ToString();  
                    }
                }
            }

            System.Diagnostics.Debug.Assert(MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null);
            if (MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null)
            {
                System.Reflection.AssemblyName assemblyName = MSWebDeploymentAssembly.DynamicAssembly.Assembly.GetName();
                System.Version minVersion = new System.Version(currentMinVersion.Major, currentMinVersion.Minor);
                System.Version assemblyVersion = assemblyName.Version; // assembly version only accurate to the minor version
                bool fMinVersionNotMeet = false;

                if (assemblyVersion < minVersion)
                {
                    fMinVersionNotMeet = true;
                }

                if (fMinVersionNotMeet)
                {
                    _strErrorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                        Resources.VSMSDEPLOY_MSDEPLOY32bit,
                        Resources.VSMSDEPLOY_MSDEPLOY64bit,
                        assemblyVersion,
                        currentMinVersion);
                    return false;
                }

                return true;
            }
            else
            {
#if NET472
                _strErrorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                 Resources.VSMSDEPLOY_MSDEPLOY32bit,
                 Resources.VSMSDEPLOY_MSDEPLOY64bit,
                 new System.Version(),
                 currentMinVersion);
#else
                _strErrorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYLOADFAIL,
                 Resources.VSMSDEPLOY_MSDEPLOY32bit,
                 Resources.VSMSDEPLOY_MSDEPLOY64bit,
                 new System.Version(3,6),
                 currentMinVersion);
#endif
                return false;
            }
        }

#if NET472
        /// <summary>
        /// Return a search path for the data
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="xmlPath"></param>
        /// <param name="defaultNamespace"></param>
        /// <returns></returns>
        public static string GetNodeFromProjectFile(Xml.XmlDocument doc, Xml.XmlNamespaceManager xmlnsManager,
            string xmlPath, string defaultNamespace)
        {
            if (doc == null)
                return null;

            string searchPath = xmlPath;
            if (!string.IsNullOrEmpty(defaultNamespace))
            {
                RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"([\w]+)");
                searchPath = regex.Replace(xmlPath, defaultNamespace + @":$1");
            }

            Xml.XmlNode xmlNode = doc.SelectSingleNode(searchPath, xmlnsManager);
            if (xmlNode != null)
            {
                return xmlNode.InnerText;
            }
            return null;
        }
#endif
        /// <summary>
        /// Utility to help build the argument list from the enum type
        /// </summary>
        /// <param name="item"></param>
        /// <param name="arguments"></param>
        /// <param name="enumType"></param>
        static internal void BuildArgumentsBaseOnEnumTypeName(Framework.ITaskItem item, System.Collections.Generic.List<string> arguments, System.Type enumType, string valueQuote)
        {
            string[] enumNames = System.Enum.GetNames(enumType);
            foreach (string enumName in enumNames)
            {
                string data = item.GetMetadata(enumName);
                if (!string.IsNullOrEmpty(data))
                {
                    string valueData = PutValueInQuote(data, valueQuote);
#if NET472
                    arguments.Add(string.Concat(enumName.ToLower(System.Globalization.CultureInfo.InvariantCulture), "=", valueData));
#else
                    arguments.Add(string.Concat(enumName.ToLower(), "=", valueData));
#endif
                }
            }
        }

        static internal string AlternativeQuote(string valueQuote)
        {
            if (string.IsNullOrEmpty(valueQuote) || valueQuote == "\"")
            {
                return "'";
            }
            else
            {
                return "\"";
            }
        }

        static public char[] s_specialCharactersForCmd = @"&()[]{}^=;!'+,`~".ToArray();
        static internal string PutValueInQuote(string value, string quote)
        {
            if (string.IsNullOrEmpty(quote))
            {
                if (value != null & value.IndexOfAny(s_specialCharactersForCmd) >= 0)
                {
                    // any command line special characters, we use doubld quote by default
                    quote = "\"";
                }
                else
                {
                    // otherwise we pick the smart one.
                    quote = AlternativeQuote(quote);
                }
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains(quote))
                    {
                        quote = AlternativeQuote(quote);
                    }
                }
                
            }
            return string.Concat(quote, value, quote);
        }


        public static bool IsOneof(string source, string[] listOfItems, System.StringComparison comparsion)
        {
            if (listOfItems != null && !string.IsNullOrEmpty(source))
            {
                foreach (string item in listOfItems)
                {
                    if (string.Compare(source, item, comparsion) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Utiilty function to prompt commom end of Execution message for msdeploy.exe
        /// 
        /// </summary>
        /// <param name="bSuccess"></param>
        /// <param name="destType"></param>
        /// <param name="destRoot"></param>
        /// <param name="Log"></param>
        public static void MsDeployExeEndOfExecuteMessage(bool bSuccess, string destType, string destRoot, Utilities.TaskLoggingHelper Log)
        {
            bool fNeedUnpackageHelpLink = false;
            string strSucceedFailMsg;
            string[] packageArchivedir = new string[] { MSDeploy.Provider.ArchiveDir, MSDeploy.Provider.Package};
            string[] ArchiveDirOnly = new string[] { MSDeploy.Provider.ArchiveDir };
            if (bSuccess)
            {
#if NET472
                if (IsOneof(destType, packageArchivedir, System.StringComparison.InvariantCultureIgnoreCase))
#else
                if (IsOneof(destType, packageArchivedir, System.StringComparison.OrdinalIgnoreCase))
#endif
                {
                    //strip off the trailing slash, so IO.Path.GetDirectoryName/GetFileName will return values correctly
                    destRoot = StripOffTrailingSlashes(destRoot);

                    string dir = IO.Path.GetDirectoryName(destRoot);
                    string dirUri = ConvertAbsPhysicalPathToAbsUriPath(dir);
#if NET472
                    if (IsOneof(destType, ArchiveDirOnly, System.StringComparison.InvariantCultureIgnoreCase))
#else
                    if (IsOneof(destType, ArchiveDirOnly, System.StringComparison.OrdinalIgnoreCase))
#endif
                        strSucceedFailMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedArchiveDir, string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    else
                        strSucceedFailMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_SucceedPackage, IO.Path.GetFileName(destRoot), string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    fNeedUnpackageHelpLink = true;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_SucceedDeploy;
                }
            }
            else
            {
#if NET472
                if (IsOneof(destType, packageArchivedir, System.StringComparison.InvariantCultureIgnoreCase))
#else
                if (IsOneof(destType, packageArchivedir, System.StringComparison.OrdinalIgnoreCase))
#endif
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedPackage;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedDeploy;
                }
            }
            Log.LogMessage(Framework.MessageImportance.High, strSucceedFailMsg);
            if (fNeedUnpackageHelpLink)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLinkMessage);
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLink);
            }
        }
      
        /// <summary>
        /// Utiilty function to prompt commom end of Execution message
        /// </summary>
        /// <param name="bSuccess"></param>
        /// <param name="destType"></param>
        /// <param name="destRoot"></param>
        /// <param name="Log"></param>
        public static void MsDeployEndOfExecuteMessage(bool bSuccess, string destType, string destRoot, Utilities.TaskLoggingHelper Log )
        {
            // Deployment.DeploymentWellKnownProvider wellKnownProvider =  Deployment.DeploymentWellKnownProvider.Unknown;
            System.Type DeploymentWellKnownProviderType = MSWebDeploymentAssembly.DynamicAssembly.GetType(MSDeploy.TypeName.DeploymentWellKnownProvider);
            dynamic wellKnownProvider = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, "Unknown");
#if NET472
            if (string.Compare(destType, MSDeploy.Provider.DbDacFx, System.StringComparison.InvariantCultureIgnoreCase) != 0)
#else
            if (string.Compare(destType, MSDeploy.Provider.DbDacFx, System.StringComparison.OrdinalIgnoreCase) != 0)
#endif
            {
                try
                {
                    wellKnownProvider = System.Enum.Parse(DeploymentWellKnownProviderType, destType, true);
                }
                catch
                {
                    // don't cause the failure;
                }
            }
            bool fNeedUnpackageHelpLink = false;
            string strSucceedFailMsg;
            if (bSuccess)
            {
                if (wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)) ||
                    wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Package)))
                {
                    //strip off the trailing slash, so IO.Path.GetDirectoryName/GetFileName will return values correctly
                    destRoot = StripOffTrailingSlashes(destRoot);

                    string dir = IO.Path.GetDirectoryName(destRoot);
                    string dirUri = ConvertAbsPhysicalPathToAbsUriPath(dir);
                    if (wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)))
                        strSucceedFailMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_SucceedArchiveDir, string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    else
                        strSucceedFailMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_SucceedPackage, IO.Path.GetFileName(destRoot), string.IsNullOrEmpty(dirUri) ? destRoot : dirUri);
                    fNeedUnpackageHelpLink = true;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_SucceedDeploy;
                }

                 
            }
            else
            {
                if (wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.ArchiveDir)) ||
                    wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Package)))
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedPackage;
                }
                else
                {
                    strSucceedFailMsg = Resources.VSMSDEPLOY_FailedDeploy;
                }
            }
            Log.LogMessage(Framework.MessageImportance.High, strSucceedFailMsg);
            if (fNeedUnpackageHelpLink)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLinkMessage);
                Log.LogMessage(Framework.MessageImportance.High, Resources.VSMSDEPLOY_WebPackageHelpLink);
            }
        }

        static public string ConvertAbsPhysicalPathToAbsUriPath(string physicalPath)
        {
            string absUriPath = string.Empty;
            try
            {
                System.Uri uri = new System.Uri(physicalPath);
                if (uri.IsAbsoluteUri)
                    absUriPath = uri.AbsoluteUri;
            }
            catch { }
            return absUriPath;
        }

        // utility function to add the replace rule for the option
        static public void AddReplaceRulesToOptions(/*Deployment.DeploymentRuleCollection*/ dynamic syncConfigRules, Framework.ITaskItem[] replaceRuleItems)
        {
            if (syncConfigRules != null && replaceRuleItems != null)// Dev10 bug 496639 foreach will throw the exception if the replaceRuleItem is null
            {
                foreach (Framework.ITaskItem item in replaceRuleItems)
                {
                    string ruleName = item.ItemSpec;
                    string objectName = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.ObjectName.ToString());
                    string matchRegularExpression = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.Match.ToString());
                    string replaceWith = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.Replace.ToString());
                    string scopeAttributeName = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.ScopeAttributeName.ToString());
                    string scopeAttributeValue = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.ScopeAttributeValue.ToString());
                    string targetAttributeName = item.GetMetadata(MsDeploy.ReplaceRuleMetadata.TargetAttributeName.ToString());

                    
                    ///*Deployment.DeploymentReplaceRule*/ dynamic replaceRule =
                    //    new Deployment.DeploymentReplaceRule(ruleName, objectName, scopeAttributeName, 
                    //        scopeAttributeValue, targetAttributeName, matchRegularExpression, replaceWith);


                    /*Deployment.DeploymentReplaceRule*/
                    dynamic replaceRule = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentReplaceRule",
                        new object[]{ruleName, objectName, scopeAttributeName,
                        scopeAttributeValue, targetAttributeName, matchRegularExpression, replaceWith});


                    syncConfigRules.Add(replaceRule);
                }
            }
        }

        /// <summary>
        /// utility function to enable the skip directive's enable state
        /// </summary>
        /// <param name="baseOptions"></param>
        /// <param name="stringList"></param>
        /// <param name="enabled"></param>
        /// <param name="log"></param>
        static internal void AdjsutSkipDirectives(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions, Generic.List<string> stringList, bool enabled, Utilities.TaskLoggingHelper log)
        {
            if (stringList != null && baseOptions != null)
            {
                foreach (string name in stringList)
                {
                    foreach (/*Deployment.DeploymentSkipDirective*/ dynamic skipDirective in baseOptions.SkipDirectives)
                    {
                        if (string.Compare(skipDirective.Name, name,   System.StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            if (skipDirective.Enabled != enabled)
                            {
                                skipDirective.Enabled = enabled;
                            }
                            log.LogMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_SkipDirectiveSetEnable, skipDirective.Name, enabled.ToString()));

                        }
                    }
                    log.LogWarning(string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_UnknownSkipDirective, name));
                }
            }
        }

        // utility function to add the skip rule for the option
        static public void AddSkipDirectiveToBaseOptions(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions , 
            Framework.ITaskItem[] skipRuleItems,
            Generic.List<string> enableSkipDirectiveList,
            Generic.List<string> disableSkipDirectiveList,
            Utilities.TaskLoggingHelper log)
        {
            if (baseOptions != null && skipRuleItems != null)
            {
                System.Collections.Generic.List<string> arguments = new System.Collections.Generic.List<string>(6);

                foreach (Framework.ITaskItem item in skipRuleItems)
                {
                    arguments.Clear();
                    MsDeploy.Utility.BuildArgumentsBaseOnEnumTypeName(item, arguments, typeof(SkipRuleMetadata), "\"");
                    if (arguments.Count > 0)
                    {
                        string name = item.ItemSpec;
                        ///*Deployment.DeploymentSkipDirective*/ dynamic skipDirective = new Microsoft.Web.Deployment.DeploymentSkipDirective(name, string.Join(",", arguments.ToArray()), true);

                        /*Deployment.DeploymentSkipDirective*/
                        dynamic skipDirective =  MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSkipDirective", new object[]{name, string.Join(",", arguments.ToArray()), true});
                        baseOptions.SkipDirectives.Add(skipDirective);
                    }
                }
                AdjsutSkipDirectives(baseOptions, enableSkipDirectiveList, true, log);
                AdjsutSkipDirectives(baseOptions, disableSkipDirectiveList, false, log);
            }
        }

    
        /// <summary>
        /// Utility to add single DeclareParameter to the list
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="item"></param>
        static public void AddDeclarParameterToOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem item)
        {
            if (item != null && vSMSDeploySyncOption != null)
            {
                string name = item.ItemSpec;
                string elemment = item.GetMetadata(MsDeploy.ExistingParameterValiationMetadata.Element.ToString());
                if (string.IsNullOrEmpty(elemment))
                    elemment = "parameterEntry";
                string kind = item.GetMetadata(MsDeploy.DeclareParameterMetadata.Kind.ToString());
                string scope = item.GetMetadata(MsDeploy.DeclareParameterMetadata.Scope.ToString());
                string matchRegularExpression = item.GetMetadata(MsDeploy.DeclareParameterMetadata.Match.ToString());
                string description = item.GetMetadata(MsDeploy.DeclareParameterMetadata.Description.ToString());
                string defaultValue = item.GetMetadata(MsDeploy.DeclareParameterMetadata.DefaultValue.ToString());
                string tags = item.GetMetadata(MsDeploy.DeclareParameterMetadata.Tags.ToString());

                dynamic deploymentSyncParameter= null;
                // the following have out argument, can't use dynamic on it
                // vSMSDeploySyncOption.DeclaredParameters.TryGetValue(name, out deploymentSyncParameter);
                MSWebDeploymentAssembly.DeploymentTryGetValueContains(vSMSDeploySyncOption.DeclaredParameters, name, out deploymentSyncParameter);

                if (deploymentSyncParameter == null)
                {
                    deploymentSyncParameter = 
                       MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameter", new object[]{name, description, defaultValue, tags});
                    vSMSDeploySyncOption.DeclaredParameters.Add(deploymentSyncParameter);
                }
                if (!string.IsNullOrEmpty(kind))
                {
                    if (string.Compare(elemment, "parameterEntry", System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        bool fAddEntry = true;
                        foreach (dynamic entry in deploymentSyncParameter.Entries)
                        {
                            if (scope.Equals(entry.Scope) &&
                                matchRegularExpression.Equals(entry.Match) &&
                                string.Compare(entry.Kind.ToString(), kind, System.StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                fAddEntry = false;
                            }
                        }
                        if (fAddEntry)
                        {
                            dynamic parameterEntry = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterEntry",
                                new object[] { kind, scope, matchRegularExpression, string.Empty });
                            deploymentSyncParameter.Add(parameterEntry);
                        }
                    }
                    else if (string.Compare(elemment, "parameterValidation", System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // this is bogus assertion because by default msdeploy always setup the validation which is never be null
                        // System.Diagnostics.Debug.Assert(deploymentSyncParameter.Validation == null, "deploymentSyncParameter.Validation is already set");
                        string validationString = item.GetMetadata(MsDeploy.ExistingParameterValiationMetadata.ValidationString.ToString());

                        object validationKindNone = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", "None");
                        dynamic validationKind = validationKindNone;
                        System.Type validationKindType = validationKind.GetType();
                        dynamic currentvalidationKind = validationKindNone;

                        string[] validationKinds = kind.Split(new char[] { ',' });

                        foreach (string strValidationKind in validationKinds)
                        {
                            if (MSWebDeploymentAssembly.DynamicAssembly.TryGetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", strValidationKind, out currentvalidationKind))
                            {
                                validationKind = System.Enum.ToObject(validationKindType, ((int)(validationKind)) | ((int)(currentvalidationKind)));
                            }
                        }
                        // dynamic doesn't compare, since this is enum, cast to int to compare
                        if ((int)validationKind != (int)validationKindNone)
                        {
                            // due to the reflection the we can't
                            // $exception	{"Cannot implicitly convert type 'object' to 'Microsoft.Web.Deployment.DeploymentSyncParameterValidation'. An explicit conversion exists (are you missing a cast?)"}	System.Exception {Microsoft.CSharp.RuntimeBinder.RuntimeBinderException}
                            object parameterValidation = 
                                MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterValidation", new object[] {validationKind, validationString});
                            SetDynamicProperty(deploymentSyncParameter, "Validation", parameterValidation);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to avoid the reflection exception when assign to a strongtype property
        /// // $exception	{"Cannot implicitly convert type 'object' to 'Microsoft.Web.Deployment.DeploymentSyncParameterValidation'. An explicit conversion exists (are you missing a cast?)"}	System.Exception {Microsoft.CSharp.RuntimeBinder.RuntimeBinderException}
        /// </summary>
        /// <param name="thisObj"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        static public void SetDynamicProperty(dynamic thisObj, string propertyName, object value)
        {
            thisObj.GetType().GetProperty(propertyName).SetValue(thisObj, value, null);
        }


        /// <summary>
        /// Utility function to add DeclareParameter in line
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        static public void AddDeclareParametersToOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem[] originalItems, bool fOptimisticPickNextDefaultValue)
        {
            System.Collections.Generic.IList<Framework.ITaskItem> items = MsDeploy.Utility.SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, MsDeploy.DeclareParameterMetadata.DefaultValue.ToString());
            if (vSMSDeploySyncOption != null && items != null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    AddDeclarParameterToOptions(vSMSDeploySyncOption, item);
                }
            }
        }


        // MSDeploy change -- Deprecate
        ///// <summary>
        ///// Utility function to support DeclarParametersFromFile
        ///// </summary>
        ///// <param name="vSMSDeploySyncOption"></param>
        ///// <param name="items"></param>
        static public void AddImportDeclareParametersFileOptions(/*VSMSDeploySyncOption*/ dynamic vSMSDeploySyncOption, Framework.ITaskItem[] items)
        {
            if (vSMSDeploySyncOption != null && items != null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string fileName = item.ItemSpec;
                    vSMSDeploySyncOption.DeclaredParameters.Load(fileName);
                }
            }
        }

        static public void AddSetParametersFilesToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, Generic.IList<string> filenames, IVSMSDeployHost host)
        {
            if (deploymentObject != null && filenames != null)
            {
                foreach (string filename in filenames)
                {
                    if (!string.IsNullOrEmpty(filename))
                    {
                        try
                        {
                            deploymentObject.SyncParameters.Load(filename);
                        }
                        catch (System.Exception e)
                        {
                            if (host != null)
                                host.Log.LogErrorFromException(e);
                            else
                                throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Utility function to set SimpleeSyncParameter Name/Value
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        static public void AddSimpleSetParametersVsMsDeployObject(MsDeploy.VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[] originalItems, bool fOptimisticPickNextDefaultValue)
        {
            System.Collections.Generic.IList<Framework.ITaskItem> items = MsDeploy.Utility.SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, MsDeploy.SimpleSyncParameterMetadata.Value.ToString());
            if (srcVsMsDeployobject != null && items != null)
            {
                string lastItemName = string.Empty;
                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    if (string.CompareOrdinal(name, lastItemName) != 0)
                    {
                        string value = item.GetMetadata(MsDeploy.SimpleSyncParameterMetadata.Value.ToString());
                        srcVsMsDeployobject.SyncParameter(name, value);
                        lastItemName = name;
                    }
                }
            }
        }

        static public void AddProviderOptions(/*Deployment.DeploymentProviderOptions*/ dynamic deploymentProviderOptions, Generic.IList<ProviderOption> providerOptions, IVSMSDeployHost host)
        {
            if (deploymentProviderOptions != null && providerOptions != null)
            {
                foreach (ProviderOption item in providerOptions)
                {
                    string factoryName = item.FactoryName;
                    string name = item.Name;
                    string value = item.Value;
                    // Error handling is not required here if the providerOptions list is different from deploymentProviderOptions.ProviderSettings.
                    // providerOptions list contains metadata from MSBuild and this may be different from deploymentProviderOptions.ProviderSettings.
                    if (string.Compare(factoryName, deploymentProviderOptions.Factory.Name, System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        dynamic setting = null;

                        // deploymentProviderOptions.ProviderSettings.TryGetValue(name, out setting);
                        MSWebDeploymentAssembly.DeploymentTryGetValueForEach(deploymentProviderOptions.ProviderSettings, name, out setting);
                        if (setting != null)
                        {
                            setting.Value = value;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Utility function to set SimpleeSyncParameter Name/Value
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        static public void AddSimpleSetParametersToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, Generic.IList<ParameterInfo> parameters, IVSMSDeployHost host)
        {
            if (deploymentObject != null && parameters != null)
            {
                Generic.Dictionary<string, string> nameValueDictionary = new System.Collections.Generic.Dictionary<string, string>(parameters.Count, System.StringComparer.OrdinalIgnoreCase);
                foreach (ParameterInfo item in parameters)
                {
                    string name = item.Name;
                    string value;
                    if (!nameValueDictionary.TryGetValue(name, out value))
                    {
                        value = item.Value;
                    }
                    
                    dynamic parameter = null;
                    // deploymentObject.SyncParameters.TryGetValue(name, out parameter);
                    MSWebDeploymentAssembly.DeploymentTryGetValueContains(deploymentObject.SyncParameters, name, out parameter);
                    string msg = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_AddParameterIntoObject, name, value, deploymentObject.Name);
                    host.Log.LogMessage(msg);
                    if (parameter != null)
                    {
                        parameter.Value = value;
                    }
                    else
                    {
                        // Try to get error message to show.
                        Text.StringBuilder sb = MsDeploy.Utility.CleanStringBuilder;
                        foreach (dynamic param in deploymentObject.SyncParameters)
                        {
                            if (sb.Length != 0)
                            {
                                sb.Append(", ");
                            }
                            sb.Append(param.Name);
                        }
                        // To do, change this to resource
                        string errMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_UnknownParameter, name, sb.ToString());
                        if (host != null)
                        {
                            throw new System.InvalidOperationException(errMessage);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Utility function to setParameters in type, scope, match, value of SyncParameter
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        static public void AddSetParametersToObject(/*Deployment.DeploymentObject*/ dynamic deploymentObject, Generic.IList<ParameterInfoWithEntry> parameters, IVSMSDeployHost host)
        {
            if (deploymentObject != null && parameters != null)
            {
                Generic.Dictionary<string, string> nameValueDictionary = new System.Collections.Generic.Dictionary<string, string>(parameters.Count, System.StringComparer.OrdinalIgnoreCase);
                Generic.Dictionary<string, string> entryIdentityDictionary = new System.Collections.Generic.Dictionary<string, string>(parameters.Count);
                
                foreach (ParameterInfoWithEntry item in parameters)
                {
                    try
                    {
                        string data = null;
                        if (!nameValueDictionary.TryGetValue(item.Name, out data))
                        {
                            nameValueDictionary.Add(item.Name, item.Value);
                            data = item.Value;
                        }

                        dynamic parameter = null;
                        dynamic parameterEntry = null;
                        dynamic parameterValidation = null;
                        if (!string.IsNullOrEmpty(item.Kind))
                        {
                            string identityString = string.Join(";", new string []{item.Name, item.Kind, item.Scope, item.Match, item.Element, item.ValidationString});
                            if (!entryIdentityDictionary.ContainsKey(identityString))
                            {
                                if (string.Compare(item.Element, "parameterEntry", System.StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    parameterEntry = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterEntry",
                                        new object[]{item.Kind, item.Scope, item.Match, string.Empty});
                                }
                                else if (string.Compare(item.Element, "parameterValidation", System.StringComparison.OrdinalIgnoreCase) == 0)
                                {

                                    // this is bogus assertion because by default msdeploy always setup the validation which is never be null
                                    // System.Diagnostics.Debug.Assert(deploymentSyncParameter.Validation == null, "deploymentSyncParameter.Validation is already set");
                                    string validationString = item.ValidationString;

                                    object validationKindNone = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", "None");
                                    dynamic validationKind = validationKindNone;
                                    System.Type validationKindType = validationKind.GetType();
                                    dynamic currentvalidationKind = validationKindNone;

                                    string[] validationKinds = item.Kind.Split(new char[] { ',' });

                                    foreach (string strValidationKind in validationKinds)
                                    {
                                        if (MSWebDeploymentAssembly.DynamicAssembly.TryGetEnumValue("Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind", strValidationKind, out currentvalidationKind))
                                        {
                                            validationKind = System.Enum.ToObject(validationKindType, ((int)(validationKind)) | ((int)(currentvalidationKind)));
                                        }
                                    }
                                    // dynamic doesn't compare, since this is enum, cast to int to compare
                                    if ((int)validationKind != (int)validationKindNone)
                                    {
                                        parameterValidation =
                                            MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameterValidation", new object[] { validationKind, validationString });
                                    }




                                    //Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind validationKind = Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind.None;
                                    //Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind currentvalidationKind;
                                    //string[] validationKinds = item.Kind.Split(new char[] { ',' });

                                    //foreach (string strValidationKind in validationKinds)
                                    //{
                                    //    if (System.Enum.TryParse<Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind>(strValidationKind, out currentvalidationKind))
                                    //    {
                                    //        validationKind |= currentvalidationKind;
                                    //    }
                                    //}

                                    //if (validationKind != Microsoft.Web.Deployment.DeploymentSyncParameterValidationKind.None)
                                    //{

                                    //    parameterValidation = new Microsoft.Web.Deployment.DeploymentSyncParameterValidation(validationKind, item.ValidationString);
                                    //}
                                }
                                entryIdentityDictionary.Add(identityString, null);
                            }
                        }

                        if (!MSWebDeploymentAssembly.DeploymentTryGetValueContains(deploymentObject.SyncParameters, item.Name, out parameter))
                        {
                            parameter = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncParameter",
                                new object[] {item.Name, item.Description, item.DefaultValue, item.Tags});
                            deploymentObject.SyncParameters.Add(parameter);
                            parameter.Value = data;
                        }
                        if (parameterEntry != null)
                        {
                            parameter.Add(parameterEntry);
                        }
                        if (parameterValidation != null)
                        {
                            // due to the reflection, compiler complain on assign a object to type without explicit convertion
                            // parameter.Validation = parameterValidation;
                            SetDynamicProperty(parameter, "Validation", parameterValidation);
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (host != null)
                            host.Log.LogErrorFromException(e);
                        else
                            throw;
                    }
                }
            }
        }



        /// <summary>
        /// Utility function to setParameters in type, scope, match, value of SyncParameter
        /// </summary>
        /// <param name="vSMSDeploySyncOption"></param>
        /// <param name="items"></param>
        static public void AddSetParametersVsMsDeployObject(MsDeploy.VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[] originalItems,  bool fOptimisticPickNextDefaultValue)
        {
            System.Collections.Generic.IList<Framework.ITaskItem> items = MsDeploy.Utility.SortParametersTaskItems(originalItems, fOptimisticPickNextDefaultValue, MsDeploy.SyncParameterMetadata.DefaultValue.ToString());
            if (srcVsMsDeployobject != null && items != null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string name = item.ItemSpec;
                    string kind = item.GetMetadata(MsDeploy.SyncParameterMetadata.Kind.ToString());
                    string scope = item.GetMetadata(MsDeploy.SyncParameterMetadata.Scope.ToString());
                    string matchRegularExpression = item.GetMetadata(MsDeploy.SyncParameterMetadata.Match.ToString());
                    string value = item.GetMetadata(MsDeploy.SyncParameterMetadata.Value.ToString());
                    string description = item.GetMetadata(MsDeploy.SyncParameterMetadata.Description.ToString());
                    string defaultValue = item.GetMetadata(MsDeploy.SyncParameterMetadata.DefaultValue.ToString());
                    string tags = item.GetMetadata(MsDeploy.SyncParameterMetadata.Tags.ToString());
                    string element = item.GetMetadata(MsDeploy.ExistingParameterValiationMetadata.Element.ToString());
                    if (string.IsNullOrEmpty(element))
                        element = "parameterEntry";
                    string validationString = item.GetMetadata(MsDeploy.ExistingParameterValiationMetadata.ValidationString.ToString());


                    if (string.IsNullOrEmpty(value))
                    {
                        value = defaultValue;
                    }

                    srcVsMsDeployobject.SyncParameter(name, value, kind, scope, matchRegularExpression, description, defaultValue, tags, element, validationString);
                    
                }
            }
        }

        static public void AddSetParametersFilesVsMsDeployObject(VSMSDeployObject srcVsMsDeployobject, Framework.ITaskItem[] items)
        {
            if (srcVsMsDeployobject != null && items != null)
            {
                foreach (Framework.ITaskItem item in items)
                {
                    string filename = item.ItemSpec;
                    srcVsMsDeployobject.SyncParameterFile(filename);
                }
            }
        }




        static public string DumpITeaskItem(Framework.ITaskItem iTaskItem)
        {
            Text.StringBuilder sb = MsDeploy.Utility.CleanStringBuilder;
            string itemspec = iTaskItem.ItemSpec;
            sb.Append("<Item Name=\"");
            sb.Append(itemspec);
            sb.Append("\">");

            foreach (string name in iTaskItem.MetadataNames)
            {
                string value = iTaskItem.GetMetadata(name);
                sb.Append(@"<");
                sb.Append(name);
                sb.Append(@">");
                sb.Append(value);
                sb.Append(@"</");
                sb.Append(name);
                sb.Append(@">");
            }
            sb.Append(@"</Item>");

            return sb.ToString();
        }



        public static bool IsDeploymentWellKnownProvider(string strProvider)
        {
#if NET472
            if (string.Compare(strProvider, MSDeploy.Provider.DbDacFx, System.StringComparison.InvariantCultureIgnoreCase) == 0)
#else
            if (string.Compare(strProvider, MSDeploy.Provider.DbDacFx, System.StringComparison.OrdinalIgnoreCase) == 0)
#endif
            {
                return true;
            }
            object DeploymentWellKnownProviderUnknown = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider , MSDeploy.Provider.Unknown) ;
            object deploymentProvider = DeploymentWellKnownProviderUnknown;
            try
            {
                deploymentProvider = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValueIgnoreCase(MSDeploy.TypeName.DeploymentWellKnownProvider, strProvider);
            }
            catch (System.Exception)
            {
            }
            return deploymentProvider != DeploymentWellKnownProviderUnknown;

        }

        /// <summary>
        /// Utility function to remove all Empty Directory
        /// </summary>
        /// <param name="dirPath"></param>
        internal static void RemoveAllEmptyDirectories(string dirPath, Utilities.TaskLoggingHelper Log)
        {
            if (!string.IsNullOrEmpty(dirPath) && IO.Directory.Exists(dirPath))
            {
                IO.DirectoryInfo dirInfo = new System.IO.DirectoryInfo(dirPath);
                RemoveAllEmptyDirectories(dirInfo, Log);
            }
        }

        internal static void RemoveAllEmptyDirectories(IO.DirectoryInfo dirinfo, Utilities.TaskLoggingHelper log)
        {
            if(dirinfo != null && dirinfo.Exists)
            {
                //Depth first search.
                foreach (IO.DirectoryInfo subDirInfo in  dirinfo.GetDirectories())
                {
                    RemoveAllEmptyDirectories(subDirInfo, log);
                }

                if (dirinfo.GetFileSystemInfos().GetLength(0) == 0)
                {
                    dirinfo.Delete();
                    if (log != null)
                    {
                        log.LogMessage(Microsoft.Build.Framework.MessageImportance.Normal, string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.BUILDTASK_RemoveEmptyDirectories_Deleting, dirinfo.FullName));
                    }
                    
                }
            }
        }

        static PriorityIndexComparer s_PriorityIndexComparer = null;
        internal static PriorityIndexComparer ParameterTaskComparer
        {
            get
            {
                if (s_PriorityIndexComparer == null)
                {
                    s_PriorityIndexComparer = new PriorityIndexComparer();
                }
                return s_PriorityIndexComparer;
            }
        }

        public static Generic.IList<Framework.ITaskItem> SortParametersTaskItems(Framework.ITaskItem[] taskItems, bool fOptimisticPickNextNonNullDefaultValue, string PropertyName)
        {
            Generic.IList<Framework.ITaskItem> sortedList = SortTaskItemsByPriority(taskItems);

            if (!fOptimisticPickNextNonNullDefaultValue || string.IsNullOrEmpty(PropertyName) || taskItems == null || taskItems.GetLength(0) <= 0)
            {
                return sortedList;
            }
            else
            {
                Generic.List<Framework.ITaskItem> optimizedValueList = new System.Collections.Generic.List<Microsoft.Build.Framework.ITaskItem>(sortedList);

                Generic.Dictionary<string, bool> FoundDictionary = new System.Collections.Generic.Dictionary<string, bool>(optimizedValueList.Count, System.StringComparer.OrdinalIgnoreCase);

                int maxCount = sortedList.Count;
                int i = 0;

                while (i < maxCount)
                {
                    int currentItemIndex = i;
                    Framework.ITaskItem item = optimizedValueList[i++];
                    string itemSpec = item.ItemSpec;
                    if (FoundDictionary.ContainsKey(itemSpec))
                    {
                        continue; // already scaned, move on to the next
                    }
                    else
                    {
                        bool fIsCurrentItemEmpty = string.IsNullOrEmpty(item.GetMetadata(PropertyName));
                        if (!fIsCurrentItemEmpty)
                        {
                            FoundDictionary[itemSpec] = true;
                            continue;
                        }
                        else
                        {
                            int next = i;
                            bool found = false;
                            while (next < maxCount)
                            {
                                Framework.ITaskItem nextitem = optimizedValueList[next++];
                                if (string.Compare(itemSpec, nextitem.ItemSpec, System.StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    string itemData = nextitem.GetMetadata(PropertyName);
                                    if (!string.IsNullOrEmpty(itemData))
                                    {
                                        // Get the data from the next best data
                                        Utilities.TaskItem newItem = new Microsoft.Build.Utilities.TaskItem(item);
                                        newItem.SetMetadata(PropertyName, itemData);
                                        optimizedValueList[currentItemIndex] = newItem;
                                        FoundDictionary[itemSpec] = true; // mark that we already fond teh item;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (!found)
                            {
                                FoundDictionary[itemSpec] = false; // mark that we scan through the item and not found anything
                            }
                        }
                    }
                }
                return optimizedValueList;
            }
        }


        static string strMsdeployFwlink1 = @"http://go.microsoft.com/fwlink/?LinkId=178034";
        static string strMsdeployFwlink2 = @"http://go.microsoft.com/fwlink/?LinkId=178035";
        static string strMsdeployFwlink3 = @"http://go.microsoft.com/fwlink/?LinkId=178036";
        static string strMsdeployFwlink4 = @"http://go.microsoft.com/fwlink/?LinkId=178587";
        static string strMsdeployFwlink5 = @"http://go.microsoft.com/fwlink/?LinkId=178589";
        static internal string strMsdeployInstallationFwdLink = @"http://go.microsoft.com/?linkid=9278654";

        static string[] strMsdeployFwlinks = { strMsdeployFwlink1, strMsdeployFwlink2, strMsdeployFwlink3, strMsdeployFwlink4, strMsdeployFwlink5 };
        
        static int ContainMsdeployFwlink(string errorMessage, out string provider)
        {
            int index = -1;
            provider = null;
            string[][] strMsDeployFwlinksArray = { strMsdeployFwlinks};
            foreach (string[] Fwlinks in strMsDeployFwlinksArray)
            {
                for (int i = 0; i < Fwlinks.Length; i++)
                {
                    string fwlink = Fwlinks[i];
                    int lastIndexOfFwLink = -1;
                    if ( (lastIndexOfFwLink =  errorMessage.LastIndexOf(fwlink, System.StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        index = i;
                        if (i == 0)
                        {
                            string subError = errorMessage.Substring(0, lastIndexOfFwLink);
                            subError = subError.Trim();
                            if ((lastIndexOfFwLink = subError.LastIndexOf(" ", System.StringComparison.Ordinal)) >= 0)
                            {
                                provider = subError.Substring(lastIndexOfFwLink + 1);
                            }
                        }
                        return index; // break out
                    }
                }
            }
            return index;
        }

        internal static bool IsType(System.Type type, System.Type checkType)
        {
#if NET472
            if (checkType!=null && 
                (type == checkType || type.IsSubclassOf(checkType)))
            {
                return true;
            }
#endif
            return false;
        }

        internal static string EnsureTrailingSlash(string str)
        {
            if (str != null && !str.EndsWith("/", System.StringComparison.Ordinal))
            {
                str += "/";
            }
            return str;
        }
        internal static string EnsureTrailingBackSlash(string str)
        {
            if (str != null && !str.EndsWith("\\", System.StringComparison.Ordinal))
            {
                str += "\\";
            }
            return str;
        }

        // Utility to log VsMsdeploy Exception 
        internal static void LogVsMsDeployException( Utilities.TaskLoggingHelper Log, System.Exception e)
        {
            if (e is System.Reflection.TargetInvocationException)
            {
                if (e.InnerException != null)
                    e = e.InnerException;
            }

            System.Text.StringBuilder strBuilder = new System.Text.StringBuilder(e.Message.Length * 4);
            System.Type t = e.GetType();
            if (IsType(t, MSWebDeploymentAssembly.DynamicAssembly.GetType( MSDeploy.TypeName.DeploymentEncryptionException)))
            {
                // dev10 695263 OGF: Encryption Error message needs more information for packaging
                strBuilder.Append(Resources.VSMSDEPLOY_EncryptionExceptionMessage);
            }
            else if (IsType(t,MSWebDelegationAssembly.DynamicAssembly.GetType(MSDeploy.TypeName.DeploymentException)))
            {
                System.Exception rootException = e;
                dynamic lastDeploymentException = e;
                while (rootException != null && rootException.InnerException != null)
                {
                    rootException = rootException.InnerException;
                    if (IsType(rootException.GetType(), MSWebDelegationAssembly.DynamicAssembly.GetType(MSDeploy.TypeName.DeploymentException)))
                        lastDeploymentException = rootException;
                }

#if NET472
                bool isWebException = rootException is System.Net.WebException;
                if (isWebException)
                {
                    System.Net.WebException webException = rootException as System.Net.WebException;

                    // 404 come in as ProtocolError
                    if (webException.Status == System.Net.WebExceptionStatus.ProtocolError)
                    {
                        if (webException.Message.LastIndexOf("401", System.StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException401Message);
                        }
                        else if (webException.Message.LastIndexOf("404", System.StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException404Message);
                        }
                        else if (webException.Message.LastIndexOf("502", System.StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException502Message);
                        }
                        else if (webException.Message.LastIndexOf("550", System.StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException550Message);
                        }
                        else if (webException.Message.LastIndexOf("551", System.StringComparison.Ordinal) >= 0)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_WebException551Message);
                        }
                    }
                    else if (webException.Status == System.Net.WebExceptionStatus.ConnectFailure)
                    {
                        strBuilder.Append(Resources.VSMSDEPLOY_WebExceptionConnectFailureMessage);
                    }
                }
                else if (rootException is System.Net.Sockets.SocketException)
                {
                    strBuilder.Append(Resources.VSMSDEPLOY_WebExceptionConnectFailureMessage);
                }
                else
                {
                    string strMsg = lastDeploymentException.Message;
                    string provider;
                    int index = ContainMsdeployFwlink(strMsg, out provider);
                    if (index >= 0)
                    {
                        object DeploymentWellKnownProviderUnknown = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.Unknown);

                        dynamic wellKnownProvider = DeploymentWellKnownProviderUnknown;
                        // fwdlink1
                        if (index == 0)
                        {
                            string srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1Message;
                            if (provider.LastIndexOf("sql", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1SQLMessage;
                            }
                            else
                            {
                                try
                                {
                                    wellKnownProvider = MSWebDeploymentAssembly.DynamicAssembly.GetEnumValueIgnoreCase(MSDeploy.TypeName.DeploymentWellKnownProvider, provider);
                                }
                                catch
                                {
                                    // don't cause the failure;
                                }

                                if (wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.MetaKey))
                                    || wellKnownProvider.Equals(MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.AppHostConfig)))
                                {
                                    srErrorMessage = Resources.VSMSDEPLOY_MsDeployExceptionFwlink1SiteMessage;
                                }
                            }
                            strBuilder.Append(string.Format(System.Globalization.CultureInfo.CurrentCulture,srErrorMessage, provider));
                        }
                        else if (index == 1)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink2Message);
                        }
                        else if (index == 2)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink3Message);
                        }
                        else if (index == 3)
                        {
                            strBuilder.Append(Resources.VSMSDEPLOY_MsDeployExceptionFwlink4Message);
                        }
                        else
                        {
                            System.Diagnostics.Debug.Assert(false, "fwdlink5 and above is not implemented");
                        }
                    }
                }
#endif
            }

            if (e.InnerException == null)
            {
                strBuilder.Append(e.Message);
                Log.LogError(string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_FailedWithException, strBuilder.ToString()));
            }
            else
            {
                // Dev10 bug we sometime need better error message to show user what do do
                System.Exception currentException = e;
                while (currentException != null)
                {
                    strBuilder.Append(System.Environment.NewLine);
                    strBuilder.Append(currentException.Message);
                    currentException = currentException.InnerException;
                }
                Log.LogError(string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_FailedWithExceptionWithDetail, e.Message, strBuilder.ToString()));
            }
            strBuilder.Append(System.Environment.NewLine);
            strBuilder.Append(e.StackTrace);
            Log.LogMessage(Framework.MessageImportance.Low, strBuilder.ToString());
        }

        public static Generic.IList<Framework.ITaskItem> SortTaskItemsByPriority(Framework.ITaskItem[] taskItems)
        {
            int count = taskItems != null? taskItems.GetLength(0): 0;
            Generic.SortedList<Generic.KeyValuePair<int, int>, Framework.ITaskItem> sortedList =
                new System.Collections.Generic.SortedList<System.Collections.Generic.KeyValuePair<int, int>, Microsoft.Build.Framework.ITaskItem>(count, ParameterTaskComparer);

            for (int i = 0; i < count; i++)
            {
                Framework.ITaskItem iTaskItem = taskItems[i];
                string priority = iTaskItem.GetMetadata("Priority");
                int iPriority = string.IsNullOrEmpty(priority)? 0: System.Convert.ToInt32(priority, System.Globalization.CultureInfo.InvariantCulture);
                sortedList.Add(new System.Collections.Generic.KeyValuePair<int, int>(iPriority, i), iTaskItem);
            }
            return sortedList.Values;
        }
        internal class PriorityIndexComparer : Generic.IComparer<Generic.KeyValuePair<int, int>>
        {
#region IComparer<KeyValuePair<int,int>> Members
            public int Compare(System.Collections.Generic.KeyValuePair<int, int> x, System.Collections.Generic.KeyValuePair<int, int> y)
            {
                if (x.Key == y.Key)
                {
                    return x.Value - y.Value;
                }
                else
                {
                    return x.Key - y.Key;
                }
            }
#endregion
        }

        static public string StripOffTrailingSlashes(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                while (str.EndsWith("\\", System.StringComparison.Ordinal) || str.EndsWith("/", System.StringComparison.Ordinal))
                    str = str.Substring(0, str.Length - 1);
             
            }
            return str;
        }

        static public string EnsureTrailingSlashes(string rootPath, char slash)
        {
            string directorySeparator = new string(slash, 1);
            string rootPathWithSlash = string.Concat(rootPath, rootPath.EndsWith(directorySeparator, System.StringComparison.Ordinal) ? string.Empty : directorySeparator);
            return rootPathWithSlash;
        }


        static public string GetFilePathResolution(string source, string sourceRootPath)
        {
            if (IO.Path.IsPathRooted(source) || string.IsNullOrEmpty(sourceRootPath))
                return source;
            else
                return IO.Path.Combine(sourceRootPath, source);
        }


        /// <summary>
        /// Utility to generate the Ipv6 string address to match with the ServerBinding string
        /// Ipv6 need the have 
        /// </summary>
        /// <param name="iPAddress"></param>
        /// <returns></returns>
        static internal string GetIPAddressString(System.Net.IPAddress iPAddress)
        {
            if (iPAddress != null)
            {
                if (iPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return iPAddress.ToString();
                else if (iPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    Text.StringBuilder stringBuilder = MsDeploy.Utility.CleanStringBuilder;
                    stringBuilder.Append("[");
                    stringBuilder.Append(iPAddress.ToString());
                    stringBuilder.Append("]");
                    return stringBuilder.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Utility function to match the IPAddress with the string in IIS ServerBinding's IPAddress
        /// </summary>
        /// <param name="IISBindingIPString"></param>
        /// <param name="iPAddresses"></param>
        /// <returns></returns>
        static internal bool MatchOneOfIPAddress(string IISBindingIPString, System.Net.IPAddress[] iPAddresses)
        {
            if (!string.IsNullOrEmpty(IISBindingIPString))
            {
                if (IISBindingIPString.Trim() == "*")
                    return true;

                foreach (System.Net.IPAddress iPAddress in iPAddresses)
                {
                    if (string.Compare(GetIPAddressString(iPAddress), IISBindingIPString, System.StringComparison.OrdinalIgnoreCase) == 0)
                        return true;
                }
            }
            return false;
        }

        static internal void SetupMSWebDeployDynamicAssemblies(string strVersionsToTry, Utilities.Task task)
        {
            // Mark the assembly version.
            // System.Version version1_1 = new System.Version("7.1");
            Generic.Dictionary<string, string> versionsList = new System.Collections.Generic.Dictionary<string,string>();
            if (!string.IsNullOrEmpty(strVersionsToTry))
            {
                foreach (string str in strVersionsToTry.Split(';'))
                {
                    versionsList[str] = str;
                }
            }

            const string MSDeploymentDllFallback = "9.0";
            versionsList[MSDeploymentDllFallback] = MSDeploymentDllFallback; 

            System.Version[] versionArray = versionsList.Values.Select(p => new System.Version(p)).ToArray();
            System.Array.Sort(versionArray);

            for (int i = versionArray.GetLength(0) - 1; i >= 0 ; i--)
            {
                System.Version version = versionArray[i];
                try
                {
                    MSWebDeploymentAssembly.SetVersion(version);

                    System.Version webDelegationAssemblyVersion = version;
#if NET472
                    if (MSWebDeploymentAssembly.DynamicAssembly != null && MSWebDeploymentAssembly.DynamicAssembly.Assembly != null)
                    {
                        foreach (System.Reflection.AssemblyName assemblyName in MSWebDeploymentAssembly.DynamicAssembly.Assembly.GetReferencedAssemblies())
                        {
                            if (string.Compare(assemblyName.Name, 0 ,  MSWebDelegationAssembly.AssemblyName, 0, MSWebDelegationAssembly.AssemblyName.Length, System.StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                webDelegationAssemblyVersion = assemblyName.Version;
                                break;
                            }
                        }
                    }
#endif
                    MSWebDelegationAssembly.SetVersion(webDelegationAssemblyVersion);
                    task.Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_MSDEPLOYVERSIONLOAD, task.ToString(), MSWebDeploymentAssembly.DynamicAssembly.AssemblyFullName));
                    task.Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_MSDEPLOYVERSIONLOAD, task.ToString(), MSWebDelegationAssembly.DynamicAssembly.AssemblyFullName));
                    return;
                }
                catch (System.Exception e)
                {
                    task.Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.BUILDTASK_FailedToLoadThisVersionMsDeployTryingTheNext, versionArray[i], e.Message));
                }
            }
            // if it not return by now, it is definite a error
            throw new System.InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_MSDEPLOYASSEMBLYLOAD_FAIL, task.ToString()));
        }


        static public string EscapeTextForMSBuildVariable(string text)
        {
            if (!string.IsNullOrEmpty(text) && text.IndexOfAny(@"$%".ToArray()) >= 0 )
            {
                System.Text.StringBuilder stringBuilder = new Text.StringBuilder(text.Length *2);
                char[] chars = text.ToCharArray();
                int i = 0;
                for (i = 0; i < chars.Count() -2; i++)
                {
                    char ch = chars[i];
                    char nextch1 = chars[i + 1];
                    char nextch2 = chars[i + 2];
                    bool fAlreadyHandled = false;
                    switch(ch)
                    {
                        case '$':
                            if (nextch1 == '(')
                            {
                                stringBuilder.Append("%24");
                                fAlreadyHandled = true;
                            }
                            break;
                        case '%':
                            if (nextch1 == '(' || ("0123456789ABCDEFabcdef".IndexOf(nextch1) >= 0 && "0123456789ABCDEFabcdef".IndexOf(nextch2) >= 0))
                            {
                                stringBuilder.Append("%25");
                                fAlreadyHandled = true;
                            }
                            break;
                    }
                    if (!fAlreadyHandled)
                    {
                        stringBuilder.Append(ch);
                    }
                }
                for (; i < chars.Count(); i++)
                    stringBuilder.Append(chars[i]);
                return stringBuilder.ToString();
            }
            return text;
        }
        /// <summary>
        /// Given a user agant string, it appends :WTE<version> to it if
        /// the string is not null.
        /// </summary>
        public static string GetFullUserAgentString(string userAgent)
        {
#if NET472
            if(string.IsNullOrEmpty(userAgent))
                return null;
            try 
            {
                object[] o = typeof(Utility).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
                if (o.Length > 0 && o[0] is AssemblyFileVersionAttribute)
                {
                    return string.Concat(userAgent, ":WTE", ((AssemblyFileVersionAttribute)o[0]).Version);
                }
            }
            catch
            {
                Debug.Assert(false, "Error getting WTE version");
            }
#endif
            return userAgent;
        }

    }


    internal static class ItemFilter
    {
        public delegate bool ItemMetadataFilter(Framework.ITaskItem iTaskItem);

        public static bool ItemFilterPipelineMetadata(Framework.ITaskItem item, string metadataName, string metadataValue, bool fIgnoreCase)
        {
#if NET472
            return (string.Compare(item.GetMetadata(metadataName), metadataValue, fIgnoreCase, CultureInfo.InvariantCulture) == 0);
#else
            return (string.Compare(item.GetMetadata(metadataName), metadataValue, fIgnoreCase) == 0);
#endif

        }

        public static bool ItemFilterExcludeTrue(Framework.ITaskItem iTaskItem)
        {
            string metadataName = PipelineMetadata.Exclude.ToString();
            return ItemFilterPipelineMetadata(iTaskItem, metadataName, "true", true);
        }

    }


}
