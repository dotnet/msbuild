// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    using Microsoft.Build.Framework;
    using Collections = System.Collections;
    using Diagnostics = System.Diagnostics;
    using Framework = Build.Framework;
    using Utilities = Build.Utilities;
    using System.Linq;
    using System.Collections.Generic;
    using Microsoft.NET.Sdk.Publish.Tasks.Properties;

    /// <summary>
    /// WrapperClass for Microsoft.Web.Deployment
    /// </summary>
    internal class MSWebDeploymentAssembly : DynamicAssembly
    {
        public MSWebDeploymentAssembly(System.Version verToLoad) :
            base(MSWebDeploymentAssembly.AssemblyName, verToLoad, "31bf3856ad364e35")
        {
        }

        static public string AssemblyName { get { return "Microsoft.Web.Deployment";}}
        static public MSWebDeploymentAssembly DynamicAssembly { get; set; }
        static public void SetVersion(System.Version version)
        {
            if (DynamicAssembly == null || DynamicAssembly.Version != version)
            {
                DynamicAssembly = new MSWebDeploymentAssembly(version);
            }
        }

        /// <summary>
        /// Utility function to help out on getting Deployment colleciton's tryGetMethod
        /// </summary>
        /// <param name="deploymentCollection"></param>
        /// <param name="name"></param>
        /// <param name="foundObject"></param>
        /// <returns></returns>
        static public bool DeploymentTryGetValueForEach(dynamic deploymentCollection, string name, out dynamic foundObject)
        {
            foundObject = null;
            if (deploymentCollection != null)
            {
                foreach (dynamic item in deploymentCollection)
                {
                    if ( string.Compare(name,  item.Name.ToString(), System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        foundObject = item;
                        return true;
                    }
                }
            }
            return false;
        }


        static public bool DeploymentTryGetValueContains(dynamic deploymentCollection, string name, out dynamic foundObject)
        {
            foundObject = null;
            if (deploymentCollection != null)
            {
                if (deploymentCollection.Contains(name))
                {
                    foundObject = deploymentCollection[name];
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// WrapperClass for Microsoft.Web.Delegation
    /// </summary>
    internal class MSWebDelegationAssembly : DynamicAssembly
    {
        public MSWebDelegationAssembly(System.Version verToLoad) :
            base(MSWebDelegationAssembly.AssemblyName, verToLoad, "31bf3856ad364e35")
        {
        }

        static public string AssemblyName { get { return "Microsoft.Web.Delegation"; } }

        static public MSWebDelegationAssembly DynamicAssembly { get; set; }
        static public void SetVersion(System.Version version)
        {
            if (DynamicAssembly == null || DynamicAssembly.Version != version)
            {
                DynamicAssembly = new MSWebDelegationAssembly(version);
            }
        }
    }


    // Microsoft.Web.Delegation

    ///--------------------------------------------------------------------
    enum DeployStatus
    {
        ReadyToDeploy,
        Deploying,
        DeployFinished,
        DeployAbandoned,
        DeployFailed
    }

    /// <summary>
    /// Encapsulte the process of interacting with MSDeploy
    /// </summary>
    abstract class BaseMSDeployDriver
    {
        protected VSMSDeployObject _dest;
        protected VSMSDeployObject _src;
        protected IVSMSDeployHost _host;
        
        protected /*VSMSDeploySyncOption*/ dynamic _option;
        protected bool _isCancelOperation = false;
        protected string _cancelMessage;

        public string TaskName
        {
            get
            {
                return (_host != null) ? _host.TaskName : string.Empty;
            }
        }

        public string HighImportanceEventTypes
        {
            get;
            set;
        }

        /// <summary>
        /// Boolean to cancel the operation
        /// (TODO: in RTM, use thread synchoronization to protect the entry(though not absoluately necessary.
        /// Need consider perf hit incurred though as msdeploy's callback will reference the value frequently)
        /// </summary>
        public bool IsCancelOperation
        {
            get { return _isCancelOperation; }
            set { 
                _isCancelOperation = value;
                if (!_isCancelOperation)
                    CancelMessage = null; // reset error age
            }
        }

        public string CancelMessage
        {
            get { return _cancelMessage; }
            set { _cancelMessage = value; }
        }
        
        /// <summary>
        /// called by the msdeploy to cancel the operation
        /// </summary>
        /// <returns></returns>
        private bool CancelCallback()
        {
            return IsCancelOperation;
        }

        protected /*VSMSDeploySyncOption*/ dynamic CreateOptionIfNeeded()
        {
            if (_option == null)
            {
                object option =  MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentSyncOptions");
#if NET472
                System.Type deploymentCancelCallbackType = MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentCancelCallback");
                object cancelCallbackDelegate = System.Delegate.CreateDelegate(deploymentCancelCallbackType, this, "CancelCallback");

                MsDeploy.Utility.SetDynamicProperty(option, "CancelCallback", cancelCallbackDelegate);

                // dynamic doesn't work with delegate. it complain on explicit cast needed from object -> DelegateType :(
                // _option.CancelCallback = cancelCallbackDelegate;
#endif
                _option = option;
            }
            return _option;
        }

#if NET472
        private System.Collections.Generic.Dictionary<string, Microsoft.Build.Framework.MessageImportance> _highImportanceEventTypes = null;
        private System.Collections.Generic.Dictionary<string, Microsoft.Build.Framework.MessageImportance> GetHighImportanceEventTypes()
        {
            if (_highImportanceEventTypes == null)
            {
                _highImportanceEventTypes = new System.Collections.Generic.Dictionary<string, Framework.MessageImportance>(System.StringComparer.InvariantCultureIgnoreCase); ;
                if (!string.IsNullOrEmpty(HighImportanceEventTypes))
                {
                    string[] typeNames = HighImportanceEventTypes.Split(new char[] { ';' }); // follow msbuild convention
                    foreach (string typeName in typeNames)
                    {
                        _highImportanceEventTypes.Add(typeName, Framework.MessageImportance.High);
                    }
                }
            }
            return _highImportanceEventTypes;
        }
#endif
        void TraceEventHandlerDynamic(object sender, dynamic e)
        {
            // throw new System.NotImplementedException();
            string msg = e.Message;
            System.Diagnostics.Trace.WriteLine("MSDeploy TraceEvent Handler is called with " + msg);
#if NET472
            LogTrace(e, GetHighImportanceEventTypes());
#endif
            //try
            //{
            //    LogTrace(e);
            //}
            //catch (Framework.LoggerException loggerException)
            //{
            //    System.OperationCanceledException operationCanceledException
            //        = loggerException.InnerException as System.OperationCanceledException;
            //    if (operationCanceledException != null)
            //    {
            //        // eat this exception and set the args
            //        // Loger is the one throw this exception. we should not log again.
            //        // _option.CancelCallback();
            //        IsCancelOperation = true;
            //        CancelMessage = operationCanceledException.Message;
            //    }
            //    else
            //    {
            //        throw; // rethrow if this is not a OperationCancelException
            //    }
            //}
        }


        /// <summary>
        /// Using MSDeploy API to invoke MSDeploy
        /// </summary>
        protected void InvokeMSdeploySync()
        {
            /*VSMSDeploySyncOption*/ dynamic option = CreateOptionIfNeeded();
            IsCancelOperation = false;

            _host.PopulateOptions(option);

            // you can reuse traceEventHandler if you know the function signuture is the same 
            System.Delegate traceEventHandler = DynamicAssembly.AddEventDeferHandler(
                _src.BaseOptions, 
                "Trace", 
                new DynamicAssembly.EventHandlerDynamicDelegate(TraceEventHandlerDynamic));
            DynamicAssembly.AddEventHandler(_dest.BaseOptions, "Trace", traceEventHandler);

            _host.UpdateDeploymentBaseOptions(_src, _dest);

            _src.SyncTo(_dest, option, _host);

            _host.ClearDeploymentBaseOptions(_src, _dest);

            DynamicAssembly.RemoveEventHandler(_src.BaseOptions, "Trace", traceEventHandler);
            DynamicAssembly.RemoveEventHandler(_dest.BaseOptions, "Trace", traceEventHandler);

            _src.ResetBaseOptions();
            _dest.ResetBaseOptions();
            
        }

        /// <summary>
        /// The end to end process to invoke MSDeploy
        /// </summary>
        /// <returns></returns>
        public void SyncThruMSDeploy()
        {
            BeforeSync();
            StartSync();
            WaitForDone();
            AfterSync();
        }

        /// <summary>
        /// Encapsulate the things be done before invoke MSDeploy
        /// </summary>
        abstract protected void BeforeSync();

        /// <summary>
        /// Encapsulate the approach to invoke the MSDeploy (same thread or in a seperate thread; ui or without ui)
        /// </summary>
        abstract protected void StartSync();

        /// <summary>
        /// Encapsulate the approach to wait for the MSDeploy done
        /// </summary>
        abstract protected void WaitForDone();

        /// <summary>
        /// Encapsulate how to report the Trace information
        /// </summary>
        /// <param name="e"></param>
        // abstract protected void LogTrace(Deployment.DeploymentTraceEventArgs e);

        abstract protected void LogTrace(dynamic e, System.Collections.Generic.IDictionary<string, Microsoft.Build.Framework.MessageImportance> customTypeLoging );

        /// <summary>
        /// Encapsulate the things to be done after the deploy is done
        /// </summary>
        abstract protected void AfterSync();

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        protected BaseMSDeployDriver(VSMSDeployObject src, VSMSDeployObject dest, IVSMSDeployHost host)
        {
            _src = src;
            _dest = dest;
            _host = host;
        }

        public static BaseMSDeployDriver CreateBaseMSDeployDriver(
            VSMSDeployObject src,
            VSMSDeployObject dest,
            IVSMSDeployHost host)
        {
            BaseMSDeployDriver bmd;
            bmd = new VSMSDeployDriverInCmd(src, dest, host);
            return bmd;
        }
    }

    /// <summary>
    /// We create CustomBuildWithPropertiesEventArgs is for the purpose of logging verious information
    /// in a IDictionary such that the MBuild handler can handle generically.
    /// </summary>
#if NET472
    [System.Serializable]
#endif
    public class CustomBuildWithPropertiesEventArgs : Framework.CustomBuildEventArgs, Collections.IDictionary
    {
        public CustomBuildWithPropertiesEventArgs() : base() { }
        public CustomBuildWithPropertiesEventArgs(string msg, string keyword, string senderName)
            : base(msg, keyword, senderName)
        {
        }
        
        Collections.Specialized.HybridDictionary m_hybridDictionary = new System.Collections.Specialized.HybridDictionary(10);
#region IDictionary Members 
        // Delegate everything to m_hybridDictionary

        public void Add(object key, object value)
        {
            m_hybridDictionary.Add(key, value);
        }

        public void Clear()
        {
            m_hybridDictionary.Clear();
        }

        public bool Contains(object key)
        {
            return m_hybridDictionary.Contains(key);
        }

        public System.Collections.IDictionaryEnumerator GetEnumerator()
        {
            return m_hybridDictionary.GetEnumerator();
        }

        public bool IsFixedSize
        {
            get { return m_hybridDictionary.IsFixedSize; }
        }

        public bool IsReadOnly
        {
            get { return m_hybridDictionary.IsReadOnly; }
        }

        public System.Collections.ICollection Keys
        {
            get { return m_hybridDictionary.Keys; }
        }

        public void Remove(object key)
        {
            m_hybridDictionary.Remove(key);
        }

        public System.Collections.ICollection Values
        {
            get { return m_hybridDictionary.Values; }
        }

        public object this[object key]
        {
            get { return m_hybridDictionary[key]; }
            set { m_hybridDictionary[key] = value; }
        }

#endregion

#region ICollection Members

        public void CopyTo(System.Array array, int index)
        {
            m_hybridDictionary.CopyTo(array, index);
        }

        public int Count
        {
            get { return m_hybridDictionary.Count; }
        }

        public bool IsSynchronized
        {
            get { return m_hybridDictionary.IsSynchronized; }
        }

        public object SyncRoot
        {
            get { return m_hybridDictionary.SyncRoot; }
        }

#endregion

#region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
#endregion
    }



    /// <summary>
    /// Deploy through msbuild in command line
    /// </summary>
    class VSMSDeployDriverInCmd : BaseMSDeployDriver
    {
        protected override void BeforeSync()
        {
            string strMsg = string.Format(System.Globalization.CultureInfo.CurrentCulture,Resources.VSMSDEPLOY_Start, _src.ToString(), _dest.ToString());
            _host.Log.LogMessage(strMsg);
        }


        // Utility function to log all public instance property to CustomerBuildEventArgs 
        private static void AddAllPropertiesToCustomBuildWithPropertyEventArgs(CustomBuildWithPropertiesEventArgs cbpEventArg,System.Object obj)
        {
#if NET472
            if (obj != null)
            {
                System.Type thisType = obj.GetType();
                cbpEventArg.Add("ArgumentType", thisType.ToString());
                System.Reflection.MemberInfo[] arrayMemberInfo = thisType.FindMembers(System.Reflection.MemberTypes.Property, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, null);
                if (arrayMemberInfo != null)
                {
                    foreach (System.Reflection.MemberInfo memberinfo in arrayMemberInfo)
                    {
                        object val = thisType.InvokeMember(memberinfo.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.GetProperty, null, obj, null, System.Globalization.CultureInfo.InvariantCulture);
                        if (val != null)
                            cbpEventArg.Add(memberinfo.Name, val);
                    }
                }
            }
#endif
        }


        ///// <summary>
        ///// Log Trace ifnormation in the command line
        ///// </summary>
        ///// <param name="e"></param>
        //protected override void LogTrace(Microsoft.Web.Deployment.DeploymentTraceEventArgs args)
        //{
        //    string strMsg = args.Message;
        //    string strEventType =  "Trace";
        //    Framework.MessageImportance messageImportance = Microsoft.Build.Framework.MessageImportance.Low;

        //    if (args is Deployment.DeploymentFileSerializationEventArgs ||
        //        args is Deployment.DeploymentPackageSerializationEventArgs ||
        //        args is Deployment.DeploymentObjectChangedEventArgs ||
        //        args is Deployment.DeploymentSyncParameterEventArgs )
        //    {
        //        //promote those message only for those event
        //        strEventType = "Action";
        //        messageImportance = Microsoft.Build.Framework.MessageImportance.High;
        //    }

        //    if (!string.IsNullOrEmpty(strMsg))
        //    {
        //         switch (args.EventLevel)
        //         {
        //             case System.Diagnostics.TraceLevel.Off:
        //                 break;
        //             case System.Diagnostics.TraceLevel.Error:
        //                 _host.Log.LogError(strMsg);
        //                 break;
        //             case System.Diagnostics.TraceLevel.Warning:
        //                 _host.Log.LogWarning(strMsg);
        //                 break;
        //             default: // Is Warning is a Normal message
        //                 _host.Log.LogMessageFromText(strMsg, messageImportance);
        //                 break;

        //         }
        //    }
        //    // additionally we fire the Custom event for the detail information
        //    CustomBuildWithPropertiesEventArgs customBuildWithPropertiesEventArg = new CustomBuildWithPropertiesEventArgs(args.Message, null, TaskName);

        //    customBuildWithPropertiesEventArg.Add("TaskName", TaskName);
        //    customBuildWithPropertiesEventArg.Add("EventType", strEventType);
        //    AddAllPropertiesToCustomBuildWithPropertyEventArgs(customBuildWithPropertiesEventArg, args);
        //    _host.BuildEngine.LogCustomEvent(customBuildWithPropertiesEventArg);
        //}


        protected override void LogTrace(dynamic args, System.Collections.Generic.IDictionary<string, Microsoft.Build.Framework.MessageImportance> customTypeLoging)
        {
            string strMsg = args.Message;
            string strEventType = "Trace";
            Framework.MessageImportance messageImportance = Microsoft.Build.Framework.MessageImportance.Low;

            System.Type argsT = args.GetType();
            if (MsDeploy.Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentFileSerializationEventArgs")) ||
                MsDeploy.Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentPackageSerializationEventArgs")) ||
                MsDeploy.Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentObjectChangedEventArgs")) ||
                MsDeploy.Utility.IsType(argsT, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentSyncParameterEventArgs")))
            {
                //promote those message only for those event
                strEventType = "Action";
                messageImportance = Microsoft.Build.Framework.MessageImportance.High;
            }
            else if (customTypeLoging != null && customTypeLoging.ContainsKey(argsT.Name))
            {
                strEventType = "Trace";
                messageImportance = customTypeLoging[argsT.Name];
            }

            if (!string.IsNullOrEmpty(strMsg))
            {
                System.Diagnostics.TraceLevel level = (System.Diagnostics.TraceLevel)System.Enum.ToObject(typeof(System.Diagnostics.TraceLevel), args.EventLevel);
                switch (level)
                {
                    case System.Diagnostics.TraceLevel.Off:
                        break;
                    case System.Diagnostics.TraceLevel.Error:
                        _host.Log.LogError(strMsg);
                        break;
                    case System.Diagnostics.TraceLevel.Warning:
                        _host.Log.LogWarning(strMsg);
                        break;
                    default: // Is Warning is a Normal message
                        _host.Log.LogMessageFromText(strMsg, messageImportance);
                        break;

                }
            }
            // additionally we fire the Custom event for the detail information
            CustomBuildWithPropertiesEventArgs customBuildWithPropertiesEventArg = new CustomBuildWithPropertiesEventArgs(args.Message, null, TaskName);

            customBuildWithPropertiesEventArg.Add("TaskName", TaskName);
            customBuildWithPropertiesEventArg.Add("EventType", strEventType);
            AddAllPropertiesToCustomBuildWithPropertyEventArgs(customBuildWithPropertiesEventArg, args);
            _host.BuildEngine.LogCustomEvent(customBuildWithPropertiesEventArg);
        }

        /// <summary>
        /// Invoke MSDeploy
        /// </summary>
        protected override void StartSync()
        {
            InvokeMSdeploySync();
        }

        /// <summary>
        /// Wait foreve if we are in the command line 
        /// </summary>
        protected override void WaitForDone() { }

        /// <summary>
        /// Log status after the deploy is done
        /// </summary>
        protected override void AfterSync()
        {
            string strMsg = Resources.VSMSDEPLOY_Succeeded;
            _host.Log.LogMessage(strMsg);
        }

        /// <summary>
        /// construct
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        /// <param name="log"></param>
        internal VSMSDeployDriverInCmd(VSMSDeployObject src, VSMSDeployObject dest, IVSMSDeployHost host)
            : base(src, dest, host)
        {
            if (host.GetProperty("HighImportanceEventTypes") != null)
                this.HighImportanceEventTypes = host.GetProperty("HighImportanceEventTypes").ToString();
        }
    }


    /// <summary>
    /// MSBuild Task VSMSDeploy to call the object through UI or not
    /// </summary>
    public class VSMSDeploy : Utilities.Task, IVSMSDeployHost, Framework.ICancelableTask
    {
        string _disableLink;
        string _enableLink;
        private string _disableSkipDirective;
        private string _enableSkipDirective;
        
        bool _result = false;
        bool _whatIf = false;
        string _deploymentTraceLevel;
        bool _useCheckSum = false;
        private int m_retryAttempts = -1;
        private int m_retryInterval = -1;

        bool _allowUntrustedCert;
        bool _skipExtraFilesOnServer=false;

        private Framework.ITaskItem[] m_sourceITaskItem = null;
        private Framework.ITaskItem[] m_destITaskItem = null;
        private Framework.ITaskItem[] m_replaceRuleItemsITaskItem = null;
        private Framework.ITaskItem[] m_skipRuleItemsITaskItem = null;
        private Framework.ITaskItem[] m_declareParameterItems = null;
        private Framework.ITaskItem[] m_importDeclareParametersItems = null;
        private Framework.ITaskItem[] m_simpleSetParamterItems = null;
        private Framework.ITaskItem[] m_importSetParametersItems = null; 
        private Framework.ITaskItem[] m_setParamterItems = null;

        private BaseMSDeployDriver m_msdeployDriver = null;

        [Framework.Required]
        public Framework.ITaskItem[] Source
        {
            get { return this.m_sourceITaskItem; }
            set { this.m_sourceITaskItem = value; }
        }

        public string HighImportanceEventTypes
        {
            get;
            set;
        }

        public Framework.ITaskItem[] Destination
        {
            get { return this.m_destITaskItem; }
            set { this.m_destITaskItem = value; }
        }

        public bool AllowUntrustedCertificate
        {
            get { return _allowUntrustedCert; }
            set { _allowUntrustedCert = value; }
        }

        public bool SkipExtraFilesOnServer
        {
            get { return _skipExtraFilesOnServer;}
            set { _skipExtraFilesOnServer = value; }
        }

        public bool WhatIf
        {
            get { return _whatIf; }
            set { _whatIf = value; }
        }



        public string DeploymentTraceLevel
        {
            get { return _deploymentTraceLevel; }
            set { _deploymentTraceLevel = value; }
        }


        public bool UseChecksum
        {
            get { return _useCheckSum; }
            set { _useCheckSum = value; }
        }

        //Sync result: Succeed or Fail
        [Framework.Output]
        public bool Result
        {
            get { return _result; }
            set { _result = value; }
        }

        /// <summary>
        /// Disable Link is a list of disable provider
        /// </summary>
        public string DisableLink
        {
            get { return _disableLink; }
            set { _disableLink = value; }
        }

        public string EnableLink
        {
            get { return _enableLink; }
            set { _enableLink = value; }
        }


        public string DisableSkipDirective
        {
            get { return _disableSkipDirective; }
            set { this._disableSkipDirective = value; }
        }

        public string EnableSkipDirective
        {
            get { return _enableSkipDirective; }
            set { this._enableSkipDirective = value; }
        }

        public int RetryAttempts
        {
            get { return this.m_retryAttempts; }
            set { this.m_retryAttempts = value; }
        }

        public int RetryInterval
        {
            get { return this.m_retryInterval; }
            set { this.m_retryInterval = value; }
        }


        public Framework.ITaskItem[] ReplaceRuleItems
        {
            get { return m_replaceRuleItemsITaskItem; }
            set { this.m_replaceRuleItemsITaskItem = value; }
        }


        public Framework.ITaskItem[] SkipRuleItems
        {
            get { return m_skipRuleItemsITaskItem; }
            set { this.m_skipRuleItemsITaskItem = value; }
        }


        public Framework.ITaskItem[] DeclareParameterItems
        {
            get { return this.m_declareParameterItems; }
            set { this.m_declareParameterItems = value; }
        }

        public bool OptimisticParameterDefaultValue { get; set; }


        public Framework.ITaskItem[] ImportDeclareParametersItems
        {
            get { return m_importDeclareParametersItems; }
            set { this.m_importDeclareParametersItems = value; }
        }

        public Framework.ITaskItem[] SimpleSetParameterItems
        {
            get { return m_simpleSetParamterItems; }
            set { this.m_simpleSetParamterItems = value; }
        }

        public Framework.ITaskItem[] ImportSetParametersItems
        {
            get { return m_importSetParametersItems; }
            set { this.m_importSetParametersItems = value; }
        }

        public Framework.ITaskItem[] SetParameterItems
        {
            get { return m_setParamterItems; }
            set { this.m_setParamterItems = value; }
        }

        public bool EnableMSDeployBackup {get;set;}

        public bool EnableMSDeployAppOffline { get; set; }

        public bool EnableMSDeployWebConfigEncryptRule {get;set;}

        private string _userAgent;
        public string UserAgent {
            get{return _userAgent;}
            set {
                if(!string.IsNullOrEmpty(value))
                {
                    _userAgent = MsDeploy.Utility.GetFullUserAgentString(value);
                }
            }
        }

        public Framework.ITaskItem[] AdditionalDestinationProviderOptions {get;set;}

        public string MSDeployVersionsToTry
        {
            get;
            set;
        }

        private bool AllowUntrustedCertCallback(object sp,
                System.Security.Cryptography.X509Certificates.X509Certificate cert,
                System.Security.Cryptography.X509Certificates.X509Chain chain,
                System.Net.Security.SslPolicyErrors problem)
        {
            if (AllowUntrustedCertificate)
            {
                return true;
            }

            return false;
        }
        private void SetupPublishRelatedProperties(ref VSMSDeployObject dest)
        {
#if NET472
            if (AllowUntrustedCertificate) 
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback
                         += new System.Net.Security.RemoteCertificateValidationCallback(AllowUntrustedCertCallback);
            }
#endif
        }

        public override bool Execute()
        {
            Result = false;

            try
            {
                MsDeploy.Utility.SetupMSWebDeployDynamicAssemblies(MSDeployVersionsToTry, this);
            }
            catch (System.Exception exception)
            {
                this.Log.LogErrorFromException(exception);
                return false; // failed the task
            }

            string errorMessage = null;
            if (!MsDeploy.Utility.CheckMSDeploymentVersion(this.Log, out errorMessage))
                return false;

            VSMSDeployObject src = null ;
            VSMSDeployObject dest = null;

            if (this.Source == null || this.Source.GetLength(0) != 1)
            {
                this.Log.LogError("Source must be 1 item");
                return false;
            }
            else
            {
                src = VSMSDeployObjectFactory.CreateVSMSDeployObject(this.Source[0]);
            }

            if (this.Destination == null || this.Destination.GetLength(0) != 1)
            {
                this.Log.LogError("Destination must be 1 item");
                return false;
            }
            else
            {
                dest = VSMSDeployObjectFactory.CreateVSMSDeployObject(this.Destination[0]);
                VSHostObject hostObj = new VSHostObject(HostObject as System.Collections.Generic.IEnumerable<Framework.ITaskItem>);
                string username, password;
                if (hostObj.ExtractCredentials(out username, out password))
                {
                    dest.UserName = username;
                    dest.Password = password;
                }
            }

            //$Todo, Should we split the Disable Link to two set of setting, one for source, one for destination
            src.DisableLinks = this.DisableLink;
            dest.DisableLinks = this.DisableLink;
            src.EnableLinks = this.EnableLink;
            dest.EnableLinks = this.EnableLink;
            if (this.RetryAttempts >= 0)
            {
                src.RetryAttempts = this.RetryAttempts;
                dest.RetryAttempts = this.RetryAttempts;
            }
            if (this.RetryInterval >= 0)
            {
                src.RetryInterval = this.RetryInterval;
                dest.RetryInterval = this.RetryInterval;
            }
            dest.UserAgent = this.UserAgent;

            SetupPublishRelatedProperties(ref dest);

            // change to use when we have MSDeploy implement the dispose method 
            BaseMSDeployDriver driver = BaseMSDeployDriver.CreateBaseMSDeployDriver(src, dest, this);
            m_msdeployDriver = driver;
            try
            {
                driver.SyncThruMSDeploy();
                Result = !driver.IsCancelOperation;
            }
            catch (System.Exception e)
            {
                if (e is System.Reflection.TargetInvocationException)
                {
                    if (e.InnerException != null)
                        e = e.InnerException;
                }

                System.Type eType = e.GetType();
                if (MsDeploy.Utility.IsType(eType, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentCanceledException")))
                {
                    Log.LogMessageFromText(Resources.VSMSDEPLOY_Canceled, Microsoft.Build.Framework.MessageImportance.High);
                }
                else if (MsDeploy.Utility.IsType(eType, MSWebDelegationAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentException"))
                    || MsDeploy.Utility.IsType(eType, MSWebDeploymentAssembly.DynamicAssembly.GetType("Microsoft.Web.Deployment.DeploymentFatalException")))
                {
                    MsDeploy.Utility.LogVsMsDeployException(Log, e);
                }
                else
                {
                    if (!driver.IsCancelOperation)
                        Log.LogError(string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.VSMSDEPLOY_FailedWithException, e.Message));
                }
            }
            finally
            {
#if NET472

                if (AllowUntrustedCertificate)
                    System.Net.ServicePointManager.ServerCertificateValidationCallback
                        -= new System.Net.Security.RemoteCertificateValidationCallback(AllowUntrustedCertCallback);
#endif
            }

            Utility.MsDeployEndOfExecuteMessage(Result, dest.Provider, dest.Root, Log);
            return Result;
        }

#region IVSMSDeployHost Members

        string IVsPublishMsBuildTaskHost.TaskName
        {
            get {
                return GetType().Name;
            }
        }

        Microsoft.Build.Utilities.TaskLoggingHelper IVsPublishMsBuildTaskHost.Log
        {
            get {
                return Log;
            }
        }

        Microsoft.Build.Framework.IBuildEngine IVsPublishMsBuildTaskHost.BuildEngine
        {
            get {
                return BuildEngine;
            }
        }

        /// <summary>
        /// Sample for skipping directories
        //   <ItemGroup>
        //        <MsDeploySkipRules Include = "SkippingWWWRoot" >
        //            <ObjectName>dirPath</ ObjectName >
        //            <AbsolutePath>wwwroot</ AbsolutePath >
        //        </MsDeploySkipRules>
        //    </ ItemGroup >
        /// </summary>
        void IVSMSDeployHost.UpdateDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject)
        {
            Collections.Generic.List<string> enableSkipDirectiveList = MSDeployUtility.ConvertStringIntoList(EnableSkipDirective);
            Collections.Generic.List<string> disableSkipDirectiveList = MSDeployUtility.ConvertStringIntoList(DisableSkipDirective);

            VSHostObject hostObject = new VSHostObject(HostObject as System.Collections.Generic.IEnumerable<Framework.ITaskItem>);
            Framework.ITaskItem[] srcSkipItems, destSkipsItems;

            // Add FileSkip rules from Host Object
            hostObject.GetFileSkips(out srcSkipItems, out destSkipsItems);
            Utility.AddSkipDirectiveToBaseOptions(srcVsMsDeployobject.BaseOptions, srcSkipItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);
            Utility.AddSkipDirectiveToBaseOptions(destVsMsDeployobject.BaseOptions, destSkipsItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);

            //Add CustomSkip Rules + AppDataSkipRules
            GetCustomAndAppDataSkips(out srcSkipItems, out destSkipsItems);
            Utility.AddSkipDirectiveToBaseOptions(srcVsMsDeployobject.BaseOptions, srcSkipItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);
            Utility.AddSkipDirectiveToBaseOptions(destVsMsDeployobject.BaseOptions, destSkipsItems, enableSkipDirectiveList, disableSkipDirectiveList, Log);

            if (!string.IsNullOrEmpty(DeploymentTraceLevel))
            {
                Diagnostics.TraceLevel deploymentTraceEventLevel =
                    (Diagnostics.TraceLevel)System.Enum.Parse(typeof(Diagnostics.TraceLevel), DeploymentTraceLevel, true);
                srcVsMsDeployobject.BaseOptions.TraceLevel = deploymentTraceEventLevel;
                destVsMsDeployobject.BaseOptions.TraceLevel = deploymentTraceEventLevel;
            }

            Utility.AddSetParametersFilesVsMsDeployObject(srcVsMsDeployobject, ImportSetParametersItems); 
            Utility.AddSimpleSetParametersVsMsDeployObject(srcVsMsDeployobject, SimpleSetParameterItems, OptimisticParameterDefaultValue);
            Utility.AddSetParametersVsMsDeployObject(srcVsMsDeployobject, SetParameterItems, OptimisticParameterDefaultValue);

            AddAdditionalProviderOptions(destVsMsDeployobject);
        }
        private void GetCustomAndAppDataSkips(out ITaskItem[] srcSkips, out ITaskItem[] destSkips)
        {
            srcSkips = null;
            destSkips = null;

            if (SkipRuleItems != null)
            {
                IEnumerable<ITaskItem> items;

                items = from item in SkipRuleItems
                        where (string.IsNullOrEmpty(item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName)) ||
                               item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName) == VSMsDeployTaskHostObject.SourceDeployObject)
                        select item;
                srcSkips = items.ToArray();

                items = from item in SkipRuleItems
                        where (string.IsNullOrEmpty(item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName)) ||
                               item.GetMetadata(VSMsDeployTaskHostObject.SkipApplyMetadataName) == VSMsDeployTaskHostObject.DestinationDeployObject)
                        select item;

                destSkips = items.ToArray();
            }
        }

        private void AddAdditionalProviderOptions(VSMSDeployObject destVsMsDeployobject)
        {
            if (AdditionalDestinationProviderOptions != null)
            {
                foreach (ITaskItem item in AdditionalDestinationProviderOptions)
                {
                    if(!string.IsNullOrEmpty(item.ItemSpec))
                    {
                        string settingName = item.GetMetadata("Name");
                        string settingValue = item.GetMetadata("Value");
                        if(!string.IsNullOrEmpty(settingName) && !string.IsNullOrEmpty(settingValue))
                            destVsMsDeployobject.BaseOptions.AddDefaultProviderSetting(item.ItemSpec, settingName, settingValue);
                    }
                }
            }
        }

        void IVSMSDeployHost.ClearDeploymentBaseOptions(VSMSDeployObject srcVsMsDeployobject, VSMSDeployObject destVsMsDeployobject)
        {
            // Nothing to do here
        }

        void IVSMSDeployHost.PopulateOptions(/*Microsoft.Web.Deployment.DeploymentSyncOptions*/ dynamic option) {
            option.WhatIf = WhatIf;
            // Add the replace rules, we should consider doing the same thing for the skip rule
            MsDeploy.Utility.AddReplaceRulesToOptions(option.Rules, ReplaceRuleItems);
            MsDeploy.Utility.AddImportDeclareParametersFileOptions(option, ImportDeclareParametersItems);
            MsDeploy.Utility.AddDeclareParametersToOptions(option, DeclareParameterItems, OptimisticParameterDefaultValue);
            
            option.UseChecksum = UseChecksum;
            option.DoNotDelete = SkipExtraFilesOnServer;
            if(EnableMSDeployBackup == false)
            {
                // We need to remove the BackupRule to work around bug DevDiv: 478647. We try catch in case
                // the rule isn't there and webdeploy throws. The documentation doesn't say what the exceptions are and the function
                // is void.
                try {
                    option.Rules.Remove("BackupRule");
                }
                catch {
                }
            }

            if (EnableMSDeployAppOffline)
            {
                AddOptionRule(option, "AppOffline", "Microsoft.Web.Deployment.AppOfflineRuleHandler");
            }

            if (EnableMSDeployWebConfigEncryptRule)
            {
                AddOptionRule(option, "EncryptWebConfig", "Microsoft.Web.Deployment.EncryptWebConfigRuleHandler");
            }
        }

#endregion

#region ICancelableTask Members

        public void Cancel()
        {
            try
            {
                if (m_msdeployDriver != null)
                {
                    //[TODO: in RTM make sure we can cancel even "m_msdeployDriver" can be null, meaning vsmsdeploy task has not initialized the deploy driver to sync]
                    //Currently there is a very slim chance that users can't cancel it if the cancel action falls into this time frame 
                    m_msdeployDriver.IsCancelOperation = true;
                }
            }
            catch (System.Exception ex) 
            {
                Diagnostics.Debug.Fail("Exception on ICancelableTask.Cancel being invoked:" + ex.Message);
            }
        }

#endregion


        public object GetProperty(string propertyName)
        {
#if NET472
            string lowerName = propertyName.ToLower(System.Globalization.CultureInfo.InvariantCulture);
#else
            string lowerName = propertyName.ToLower();
#endif
            switch (lowerName)
            {
                case "msdeployversionstotry":
                    return this.MSDeployVersionsToTry;
                case "highimportanceeventtypes":
                    return this.HighImportanceEventTypes;
                default:
                    break;
            }
            return null;
        }

        public void AddOptionRule(/*Microsoft.Web.Deployment.DeploymentSyncOptions*/ dynamic option, string ruleName, string handlerType)
        {

            bool ruleExists = false;
            try
            {
                object existingRule = option.Rules[ruleName];
                ruleExists = true;
            }
            catch (Collections.Generic.KeyNotFoundException){ }

            if (!ruleExists)
            {
                dynamic appOfflineRuleHanlder = MSWebDeploymentAssembly.DynamicAssembly.CreateObject(handlerType, new object[]{});
                dynamic appOfflineRule = MSWebDeploymentAssembly.DynamicAssembly.CreateObject("Microsoft.Web.Deployment.DeploymentRule",
                    new object[] { ruleName, appOfflineRuleHanlder });
                option.Rules.Add(appOfflineRule);
            }
        }
    }
}
