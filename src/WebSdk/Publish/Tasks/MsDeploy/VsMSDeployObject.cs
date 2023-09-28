// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// VSMSDeployObject.cs
///
/// Common utility function
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------
namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using System;
    using Microsoft.NET.Sdk.Publish.Tasks.Properties;
    using Diagnostics = System.Diagnostics;
    using Generic = System.Collections.Generic;
    // using Deployment = Microsoft.Web.Deployment;
    using RegularExpressions = System.Text.RegularExpressions;

    // we need to think of a way to split the MSDeployment to other dll
    // using VSMSDeploySyncOption = Deployment.DeploymentSyncOptions;


    static class VSMSDeployObjectFactory
    {
        /// <summary>
        /// Create a object base on msbuild task item
        /// </summary>
        /// <param name="taskItem"></param>
        /// <returns></returns>
        public static VSMSDeployObject CreateVSMSDeployObject(Build.Framework.ITaskItem taskItem)
        {
            VSMSDeployObject src = new(taskItem);
            return src;
        }

        /// <summary>
        /// Create a simple object (no password)
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static VSMSDeployObject CreateVSMSDeployObject(string provider, string path)
        {
            VSMSDeployObject src = new(provider, path);
            return src;
        }

    }

    /// <summary>
    /// Utility class to abstract the multiple MSDeploy object for various secnario
    /// It also make sure the Dispose is called properly for MSDeploy object 
    /// </summary>
    internal static class MSDeployUtility
    {
        /// <summary>
        /// Utility function to create DeploymentBaseOptions base on current vsMsDeployObject
        /// </summary>
        /// <param name="vSMSDeployObject"></param>
        /// <returns></returns>
        public static /*Deployment.DeploymentBaseOptions*/ dynamic CreateBaseOptions(VSMSDeployObject vSMSDeployObject)
        {
            // /*Deployment.DeploymentBaseOptions*/dynamic baseOptions = new Microsoft.Web.Deployment.DeploymentBaseOptions();
            /*Deployment.DeploymentBaseOptions*/
            dynamic baseOptions = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentBaseOptions");

            if (vSMSDeployObject.IsLocal)
            {
                // do nothing
            }
            else if (!vSMSDeployObject.UseSeparatedCredential)
            {
                baseOptions.ComputerName = vSMSDeployObject.ComputerName;
            }
            else
            {
                baseOptions.ComputerName = vSMSDeployObject.ComputerName;
                baseOptions.UserName = vSMSDeployObject.UserName;
                baseOptions.Password = vSMSDeployObject.Password;
            }

            baseOptions.PrefetchPayload = vSMSDeployObject.PrefetchPayload;
            baseOptions.IncludeAcls = vSMSDeployObject.IncludeAcls;
            if (!string.IsNullOrEmpty(vSMSDeployObject.AuthenticationType))
            {
                baseOptions.AuthenticationType = vSMSDeployObject.AuthenticationType;
            }

            if (string.Equals(Guid.Empty.ToString(), vSMSDeployObject.UserName, StringComparison.OrdinalIgnoreCase))
            {
                baseOptions.AuthenticationType = "Bearer";
            }

            if (!string.IsNullOrEmpty(vSMSDeployObject.EncryptPassword))
                baseOptions.EncryptPassword = vSMSDeployObject.EncryptPassword;

            if (!string.IsNullOrEmpty(vSMSDeployObject.WebServerManifest))
                baseOptions.WebServerConfiguration.WebServerManifest = Path.GetFileName(vSMSDeployObject.WebServerManifest);
            if (!string.IsNullOrEmpty(vSMSDeployObject.WebServerDirectory))
                baseOptions.WebServerConfiguration.WebServerDirectory = vSMSDeployObject.WebServerDirectory;

            if (!string.IsNullOrEmpty(vSMSDeployObject.WebServerAppHostConfigDirectory))
                baseOptions.WebServerConfiguration.ConfigurationDirectory = vSMSDeployObject.WebServerAppHostConfigDirectory;


            if (vSMSDeployObject.RetryInterval >= 0)
                baseOptions.RetryInterval = vSMSDeployObject.RetryInterval;
            if (vSMSDeployObject.RetryAttempts >= 0)
                baseOptions.RetryAttempts = vSMSDeployObject.RetryAttempts;

            if (!string.IsNullOrEmpty(vSMSDeployObject.UserAgent))
                baseOptions.UserAgent = vSMSDeployObject.UserAgent;

            //remove duplicate items appearing in both "EnableLinks" and "DisableLinks" caused by the default value set by publish target file
            Generic.List<string> enabledLinkList = ConvertStringIntoList(vSMSDeployObject.EnableLinks);
            Generic.List<string> disabledLinkList = ConvertStringIntoList(vSMSDeployObject.DisableLinks);
            foreach (string link in disabledLinkList)
            {
                if (LinkContainedInTheCollection(link, enabledLinkList))
                    enabledLinkList.Remove(link);
            }

            ChangeLinkExtensionEnableStatue(baseOptions, disabledLinkList, false);
            ChangeLinkExtensionEnableStatue(baseOptions, enabledLinkList, true);

            return baseOptions;
        }

        /// <summary>
        /// Utility function to convert a string passed in from target file into a list
        /// </summary>
        /// <param name="linkExtensionsString"></param>
        /// <returns></returns>
        internal static Generic.List<string> ConvertStringIntoList(string linkExtensionsString)
        {
            string linkExtensionsInfo = "";
            if (!string.IsNullOrEmpty(linkExtensionsString))
            {
                linkExtensionsInfo = linkExtensionsString;
                string[] linksArray = linkExtensionsInfo.Split(new char[] { ';' });
                Generic.List<string> linksList = new(linksArray);
                return linksList;
            }
            else
                return new System.Collections.Generic.List<string>(0);

        }

        /// <summary>
        /// we can't use the method of List<string>.Contains, as it is case sensitive, so have to write a separate comparison routine
        /// </summary>
        /// <param name="link"></param>
        /// <param name="linkCollection"></param>
        /// <returns></returns>
        internal static bool LinkContainedInTheCollection(string link, Generic.List<string> linkCollection)
        {
            foreach (string l in linkCollection)
                if (string.Compare(l, link, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Utility function to enable a list of LinkExtensions
        /// </summary>
        /// <param name="baseOptions"></param>
        /// <param name="listOfLinkExtensions"></param>
        /// <param name="enable"></param>
        public static void ChangeLinkExtensionEnableStatue(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions, string listOfLinkExtensions, bool enable)
        {
            if (!string.IsNullOrEmpty(listOfLinkExtensions))
            {
                Generic.List<string> linkExtensionList = ConvertStringIntoList(listOfLinkExtensions);
                ChangeLinkExtensionEnableStatue(baseOptions, linkExtensionList, enable);
            }
        }

        /// <summary>
        /// Utility function to enable a list of LinkExtensions
        /// </summary>
        /// <param name="baseOptions"></param>
        /// <param name="linkExtensions"></param>
        /// <param name="enable"></param>
        public static void ChangeLinkExtensionEnableStatue(/*Deployment.DeploymentBaseOptions*/ dynamic baseOptions, System.Collections.Generic.List<string> linkExtensions, bool enable)
        {
            if (linkExtensions != null && linkExtensions.Count != 0)
            {
                foreach (string linkExtObj in linkExtensions)
                {

                    RegularExpressions.Regex match = new(linkExtObj, RegularExpressions.RegexOptions.IgnoreCase);
                    Generic.List<object> matchedList = new();

                    foreach (/*Deployment.DeploymentLinkExtension*/dynamic linkExtension in baseOptions.LinkExtensions)
                    {
                        if (match.IsMatch(linkExtension.Name))
                        {
                            matchedList.Add(linkExtension);
                        }
                    }

                    if (matchedList.Count > 0)
                    {
                        foreach (/*Deployment.DeploymentLinkExtension*/dynamic extension in matchedList)
                        {
                            extension.Enabled = enable;
                        }
                    }
                    else
                    {
                        // throw new DeploymentException(Resources.UnknownLinkExtension, disableLink);
                        //$Todo lmchen
                        //Diagnostics.Debug.Assert(false, "NYI, we should prompt user for invalid LinkExtension");
                        throw new System.InvalidOperationException("UnknowLinkExtension");
                    }
                }
            }
        }
    }
    /// <summary>
    /// Abstract interface to allow homogenious SynTo() operation to work regardless of the object
    /// </summary>
    internal class VSMSDeployObject
    {

        public VSMSDeployObject(string provider, string root)
        {
            m_NameValueDictionary.Clear();
            m_root = string.IsNullOrEmpty(root) ? string.Empty : root;

            // our code path should only take a well known provider
            Diagnostics.Debug.Assert(Utility.IsDeploymentWellKnownProvider(provider));
            m_provider = provider;

            // maybe we should show the "secure data to display"
            // for now just supress it.
#if NET472
            if (0 == string.Compare(m_provider, MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.DBFullSql).ToString(), StringComparison.InvariantCultureIgnoreCase)
                || 0 == string.Compare(m_provider, MSDeploy.Provider.DbDacFx , StringComparison.InvariantCultureIgnoreCase))
                m_fNoDisplayRoot = true;
#else
            if (0 == string.Compare(m_provider, MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.DBFullSql).ToString())
                || 0 == string.Compare(m_provider, MSDeploy.Provider.DbDacFx, StringComparison.OrdinalIgnoreCase))
                m_fNoDisplayRoot = true;
#endif
        }

        public VSMSDeployObject(Build.Framework.ITaskItem taskItem)
        {
            Diagnostics.Debug.Assert(taskItem != null);

            m_provider = taskItem.ItemSpec;
            m_root = taskItem.GetMetadata("Path");
            if (string.IsNullOrEmpty(m_root))
                m_root = string.Empty;

            // our code path should only take a well known provider
            Diagnostics.Debug.Assert(Utility.IsDeploymentWellKnownProvider(m_provider));

            // maybe we should show the "secure data to display"
            // for now just supress it.
            if (0 == string.Compare(m_provider, MSWebDeploymentAssembly.DynamicAssembly.GetEnumValue(MSDeploy.TypeName.DeploymentWellKnownProvider, MSDeploy.Provider.DBFullSql).ToString(), StringComparison.OrdinalIgnoreCase))
                m_fNoDisplayRoot = true;

            m_NameValueDictionary.Clear();
            foreach (string name in taskItem.MetadataNames)
            {
                if (!Utility.IsInternalMsdeployWellKnownItemMetadata(name))
                {
                    string value = taskItem.GetMetadata(name);
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (Utility.IsMsDeployWellKnownLocationInfo(name))
                        {
                            m_NameValueDictionary.Add(name, value);
                        }
                        else
                        {
                            // these are provider option
                            SetProviderOption(m_provider, name, value);
                        }
                    }
                }
                else
                {
                    MsDeploy.Utility.IISExpressMetadata expressMetadata;
                    if (Enum.TryParse<MsDeploy.Utility.IISExpressMetadata>(name, out expressMetadata))
                    {
                        string value = taskItem.GetMetadata(name);
                        if (!string.IsNullOrEmpty(value))
                        {
                            m_NameValueDictionary.Add(expressMetadata.ToString(), value);
                        }
                    }

                }
            }

        }

        public VSMSDeployObject(Build.Framework.ITaskItem taskItem, bool fNoDisplayRoot)
            : this(taskItem)
        {
            m_fNoDisplayRoot = fNoDisplayRoot;
        }

        private string GetDictionaryValue(string name)
        {
            string value = null;
            if (m_NameValueDictionary != null)
            {
                m_NameValueDictionary.TryGetValue(name, out value);
            }
            return value;
        }
        private void SetDictionaryValue(string name, string value)
        {
            Diagnostics.Debug.Assert(m_NameValueDictionary != null);
            if (m_NameValueDictionary.ContainsKey(name))
            {
                m_NameValueDictionary[name] = value;
            }
            else
            {
                m_NameValueDictionary.Add(name, value);
            }
        }

        protected string m_root = string.Empty;
        protected string m_disableLinks = string.Empty;
        protected string m_enableLinks = string.Empty;
        protected string m_provider = "Package";
        protected bool m_fNoDisplayRoot = false;
        protected int m_retryInterval = -1;
        protected int m_retryAttempts = -1;

        Generic.IList<MsDeploy.ParameterInfo> m_iListParameter = new Generic.List<MsDeploy.ParameterInfo>();
        Generic.IList<MsDeploy.ProviderOption> m_iListProviderOption = new Generic.List<MsDeploy.ProviderOption>();
        Generic.IList<MsDeploy.ParameterInfoWithEntry> m_iListParameterWithEntry = new Generic.List<MsDeploy.ParameterInfoWithEntry>();
        Generic.IList<string> m_iListSetParametersFiles = new Generic.List<string>();

        private System.Collections.Generic.Dictionary<string, string> m_NameValueDictionary = new(10, StringComparer.OrdinalIgnoreCase);

        protected /*Deployment.DeploymentBaseOptions*/ dynamic m_deploymentBaseOptions = null;

        public override string ToString()
        {
            string root = m_fNoDisplayRoot ? "******" : m_root;
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_ObjectIdentity, m_provider.ToString(), root);
        }


        // property used to call Deployment.DeploymentManager.CreateObject
        public virtual string Root
        {
            get { return m_root; }
            set { m_root = value; }
        }
        public virtual string Provider
        {
            get { return m_provider; }
            set { m_provider = value; }
        }


        // property use to create the LocationInfo
        public virtual bool IsLocal
        {
            get { return string.IsNullOrEmpty(ComputerName) && string.IsNullOrEmpty(MSDeployServiceUrl); }

        }
        public virtual bool UseSeparatedCredential
        {
            get { return !string.IsNullOrEmpty(UserName); }
        }


        public virtual string DisableLinks
        {
            get { return m_disableLinks; }
            set { m_disableLinks = value; }
        }

        public virtual string EnableLinks
        {
            get { return m_enableLinks; }
            set { m_enableLinks = value; }
        }



        //   <ComputerName></ComputerName>
        //<Wmsvc></Wmsvc>   -------------------------// bugbug, not supported yet
        //<UserName></UserName>
        //<Password></Password>
        //<EncryptPassword></EncryptPassword>
        //<IncludeAcls></IncludeAcls>
        //<authType></authType>
        //<prefetchPayload></prefetchPayload>


        public virtual string ComputerName
        {
            get { return GetDictionaryValue("computerName"); }
            set { SetDictionaryValue("computerName", value); }
        }
        public virtual string UserName
        {
            get { return GetDictionaryValue("userName"); }
            set { SetDictionaryValue("userName", value); }
        }

        public virtual string Password
        {
            get { return GetDictionaryValue("password"); }
            set { SetDictionaryValue("password", value); }
        }

        // Note this support is still broken for vsmsdeploy
        public string MSDeployServiceUrl
        {
            get
            {
                string value = GetDictionaryValue("wmsvc");
                Diagnostics.Debug.Assert(string.IsNullOrEmpty(value), "Not yet implement");
                return value;
            }
            set
            {
                Diagnostics.Debug.Assert(false, "Not yet implement");
                SetDictionaryValue("wmsvc", value);
            }
        }

        public string AuthenticationType
        {
            get
            {
                string authType = GetDictionaryValue("authType");
                if (string.IsNullOrEmpty(authType))
                {
                    if (!string.IsNullOrEmpty(MSDeployServiceUrl) && string.IsNullOrEmpty(ComputerName))
                    {
                        authType = "Basic";
                    }
                }
                return authType;
            }

            set { SetDictionaryValue("authType", value); }
        }

        public string EncryptPassword
        {
            get { return GetDictionaryValue("encryptPassword"); }
            set { SetDictionaryValue("encryptPassword", value); }
        }
        public bool IncludeAcls
        {
            get { return Convert.ToBoolean(GetDictionaryValue("includeAcls"), System.Globalization.CultureInfo.InvariantCulture); }
            set { SetDictionaryValue("includeAcls", value.ToString()); }
        }

        public bool PrefetchPayload
        {
            get { return Convert.ToBoolean(GetDictionaryValue("prefetchPayload"), System.Globalization.CultureInfo.InvariantCulture); }
            set { SetDictionaryValue("prefetchPayload", value.ToString()); }
        }


        public string WebServerAppHostConfigDirectory
        {
            get { return GetDictionaryValue("WebServerAppHostConfigDirectory"); }
            set { SetDictionaryValue("WebServerAppHostConfigDirectory", value); }
        }

        public string WebServerDirectory
        {
            get { return GetDictionaryValue("WebServerDirectory"); }
            set { SetDictionaryValue("WebServerDirectory", value); }
        }

        public string WebServerManifest
        {
            get { return GetDictionaryValue("WebServerManifest"); }
            set { SetDictionaryValue("WebServerManifest", value); }
        }



        public int RetryAttempts
        {
            get { return m_retryAttempts; }
            set { m_retryAttempts = value; }
        }

        public int RetryInterval
        {
            get { return m_retryInterval; }
            set { m_retryInterval = value; }
        }

        public string UserAgent { get; set; }



        public Generic.IList<MsDeploy.ParameterInfo> Parameters
        {
            get { return m_iListParameter; }
        }

        public Generic.IList<MsDeploy.ProviderOption> ProviderOptions
        {
            get { return m_iListProviderOption; }
        }

        public void SetProviderOption(string factoryName, string parameterName, string parameterStringValue)
        {
            m_iListProviderOption.Add(new ProviderOption(factoryName, parameterName, parameterStringValue));
        }

        public void SyncParameter(string parameterName, string parameterStringValue)
        {
            m_iListParameter.Add(new ParameterInfo(parameterName, parameterStringValue));
        }


        public Generic.IList<ParameterInfoWithEntry> EntryParameters
        {
            get { return m_iListParameterWithEntry; }
        }

        public Generic.IList<string> SetParametersFiles
        {
            get { return m_iListSetParametersFiles; }
        }


        public void SyncParameter(string name, string value, string type, string scope, string matchRegularExpression, string description, string defaultValue, string tags, string element, string validationString)
        {
            m_iListParameterWithEntry.Add(new MsDeploy.ParameterInfoWithEntry(name, value, type, scope, matchRegularExpression, description, defaultValue, tags, element, validationString));
        }

        public void SyncParameterFile(string filename)
        {
            m_iListSetParametersFiles.Add(filename);
        }


        public void ResetBaseOptions()
        {
            m_deploymentBaseOptions = null;
        }

        public /*Deployment.DeploymentBaseOptions*/ dynamic BaseOptions
        {
            get
            {
                if (m_deploymentBaseOptions == null)
                {
                    m_deploymentBaseOptions = MSDeployUtility.CreateBaseOptions(this);
                }
                return m_deploymentBaseOptions;
            }
        }


        public void SyncTo(VSMSDeployObject destObject, /*VSMSDeploySyncOption*/ dynamic syncOptions, IVSMSDeployHost _host)
        {
#if NET472
            //$BUGBUG lmchen, there is only set to source provider?
            // set up the provider setting
            /*Deployment.DeploymentProviderOptions*/
            dynamic srcProviderConfig = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentProviderOptions", new object[]{Provider.ToString()});
            srcProviderConfig.Path = Root;
            Utility.AddProviderOptions(srcProviderConfig, ProviderOptions, _host);

            using (/*Deployment.DeploymentObject*/ dynamic srcObj =  MSWebDeploymentAssembly.DynamicAssembly.CallStaticMethod("Microsoft.Web.Deployment.DeploymentManager", "CreateObject", new object[]{srcProviderConfig, BaseOptions}))
            {

                //$BUGBUG lmchen, there is only set to source provider?
                // set up the parameter
                Utility.AddSetParametersFilesToObject(srcObj, SetParametersFiles, _host);
                Utility.AddSimpleSetParametersToObject(srcObj, Parameters, _host);
                Utility.AddSetParametersToObject(srcObj, EntryParameters, _host);
                
                /*Deployment.DeploymentProviderOptions*/ dynamic destProviderConfig = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentProviderOptions", new object[]{destObject.Provider.ToString()});
                destProviderConfig.Path = destObject.Root;

                // Setup Destination Provider otpion
                Utility.AddProviderOptions(destProviderConfig, destObject.ProviderOptions, _host);

                srcObj.SyncTo(destProviderConfig, destObject.BaseOptions, syncOptions);
            }
#endif
        }
    }
}
