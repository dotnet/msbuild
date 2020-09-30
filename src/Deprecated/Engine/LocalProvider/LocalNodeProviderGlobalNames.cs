// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is shared between LocalNode and LocalNodeProvider and contains all the global name generation logic
    /// </summary>
    static class LocalNodeProviderGlobalNames
    {
        #region Methods

        #region Names used for startup and shutdown communication between node and the node provider
        /// <summary>
        /// If this event is set the node host process is currently running
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeActiveEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_ActiveReady_" + nodePostfix;
        }

        /// <summary>
        /// If this event is set the node is currently running a build
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeInUseEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_InUse_" + nodePostfix;
        }

        /// <summary>
        /// If this event is set the node will immediatelly exit. The event is used by the
        /// parent engine to cause the node to exit if communication is lost.
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeErrorShutdownEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_ErrorShutdown_" + nodePostfix;
        }

        /// <summary>
        /// If this event exists the node is reserved for use by a particular parent engine
        /// the node keeps a handle to this event during builds to prevent it from being used
        /// by another parent engine if the original dies
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeReserveEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_ProviderMutex_" + nodePostfix;
        }

        /// <summary>
        /// This event is used to signal to the node to create its shared memory buffers. It is used
        /// to prevent squating attacks by ensuring the both end points (child and parent) have
        /// same privilege levels
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        internal static string NodeInitiateActivationEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_InitiateActivation_" + nodePostfix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        internal static string NodeActivedEventName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_Activated_" + nodePostfix;
        }

        #endregion

        #region Names used for shared memory communication
        /// <summary>
        /// The name of the shared memory from the parent to the node
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeInputMemoryName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_In_SharedMemory_" + nodePostfix;
        }

        /// <summary>
        /// The name of the shared memory from the node to the parent
        /// </summary>
        /// <param name="nodeNumber"></param>
        /// <returns></returns>
        static internal string NodeOutputMemoryName(int nodeNumber)
        {
            if (nodePostfix == null)
            {
                InitializeGlobalNamePostFixValues();
            }
            return "Node_" + nodeNumber + "_Out_SharedMemory_" + nodePostfix;
        }

        #endregion

        /// <summary>
        /// Use reflection to figure out the version of Microsoft.Build.Engine.dll
        /// </summary>
        static private void InitializeGlobalNamePostFixValues()
        {
            AssemblyName name = new AssemblyName(Assembly.GetExecutingAssembly().FullName);
            string engineVersion = name.Version.ToString();
            string accountTypePostfix;
            if (NativeMethods.IsUserAdministrator())
            {
                accountTypePostfix = "Admin";
            }
            else
            {
                accountTypePostfix = "NotAdmin";
            }
            // As per the msdn docs for WindowsIdentity.Name Property The logon name is in the form DOMAIN\USERNAME. so replace the \ so it is not confused as a path
            string usernamePostFix = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Replace("\\", "_");

            nodePostfix = engineVersion + accountTypePostfix + usernamePostFix;
        }

        #endregion

        #region Data
        static string nodePostfix = null;
        #endregion
    }
}
