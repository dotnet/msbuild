// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This is a helper class to install an AssemblyResolver event handler in whatever AppDomain this class is created in.
    /// </summary>
    /// <owner>lukaszg</owner>
    internal class TaskEngineAssemblyResolver : MarshalByRefObject
    {
        /// <summary>
        /// This public default constructor is needed so that instances of this class can be created by NDP.
        /// </summary>
        /// <owner>lukasz</owner>
        internal TaskEngineAssemblyResolver()
        {
            // do nothing
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <owner>lukaszg</owner>
        /// <param name="taskAssemblyFileToResolve"></param>
        internal void Initialize(string taskAssemblyFileToResolve)
        {
            this.taskAssemblyFile = taskAssemblyFileToResolve;
        }

        /// <summary>
        /// Installs an AssemblyResolve handler in the current AppDomain. This class can be created in any AppDomain,
        /// so it's possible to create an AppDomain, create an instance of this class in it and use this method to install
        /// an event handler in that AppDomain. Since the event handler instance is stored internally, this method
        /// should only be called once before a corresponding call to RemoveHandler (not that it would make sense to do
        /// anything else).
        /// </summary>
        /// <owner>lukaszg</owner>
        internal void InstallHandler()
        {
            Debug.Assert(eventHandler == null, "The TaskEngineAssemblyResolver.InstallHandler method should only be called once!");

            eventHandler = new ResolveEventHandler(ResolveAssembly);
            AppDomain.CurrentDomain.AssemblyResolve += eventHandler;
        }

        /// <summary>
        /// Removes the event handler.
        /// </summary>
        /// <owner>lukaszg</owner>
        internal void RemoveHandler()
        {
            if (eventHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= eventHandler;
                eventHandler = null;
            }
            else
            {
                Debug.Assert(false, "There is no handler to remove.");
            }
        }

        /// <summary>
        /// This is an assembly resolution handler necessary for fixing up types instantiated in different
        /// AppDomains and loaded with a Assembly.LoadFrom equivalent call. See comments in TaskEngine.ExecuteTask
        /// for more details.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <owner>lukaszg</owner>
        internal Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            // Is this our task assembly?
            if (taskAssemblyFile != null)
            {
                if (File.Exists(taskAssemblyFile))
                {
                    try
                    {
                        AssemblyNameExtension taskAssemblyName = new AssemblyNameExtension(AssemblyName.GetAssemblyName(taskAssemblyFile));
                        AssemblyNameExtension argAssemblyName = new AssemblyNameExtension(args.Name);

                        if (taskAssemblyName.Equals(argAssemblyName))
                        {
                            return Assembly.UnsafeLoadFrom(taskAssemblyFile);
                        }
                    }
                    // any problems with the task assembly? return null.
                    catch (FileNotFoundException )
                    {
                        return null;
                    }
                    catch (BadImageFormatException )
                    {
                        return null;
                    }
                }
            }

            // otherwise, have a nice day.
            return null;
        }

        /// <summary>
        /// Overridden to give this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and instances can expire if they take long time processing.
        /// </summary>
        [System.Security.SecurityCritical]
        public override object InitializeLifetimeService()
        {
            // null means infinite lease time
            return null;
        }

        // path to the task assembly, but only if it's loaded using LoadFrom. If it's loaded with Load, this is null.
        private string taskAssemblyFile = null;

        // we have to store the event handler instance in case we have to remove it
        private ResolveEventHandler eventHandler = null;
    }
}
