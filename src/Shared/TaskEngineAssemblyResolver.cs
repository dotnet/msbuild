﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// This is a helper class to install an AssemblyResolver event handler in whatever AppDomain this class is created in.
    /// </summary>
    internal class TaskEngineAssemblyResolver : MarshalByRefObject
    {
        /// <summary>
        /// This public default constructor is needed so that instances of this class can be created by NDP.
        /// </summary>
        internal TaskEngineAssemblyResolver()
        {
            // do nothing
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="taskAssemblyFileToResolve"></param>
        internal void Initialize(string taskAssemblyFileToResolve)
        {
            _taskAssemblyFile = taskAssemblyFileToResolve;
        }

        /// <summary>
        /// Installs an AssemblyResolve handler in the current AppDomain. This class can be created in any AppDomain,
        /// so it's possible to create an AppDomain, create an instance of this class in it and use this method to install
        /// an event handler in that AppDomain. Since the event handler instance is stored internally, this method
        /// should only be called once before a corresponding call to RemoveHandler (not that it would make sense to do
        /// anything else).
        /// </summary>
        internal void InstallHandler()
        {
            Debug.Assert(_eventHandler == null, "The TaskEngineAssemblyResolver.InstallHandler method should only be called once!");

            _eventHandler = new ResolveEventHandler(ResolveAssembly);

            AppDomain.CurrentDomain.AssemblyResolve += _eventHandler;
        }



        /// <summary>
        /// Removes the event handler.
        /// </summary>
        internal void RemoveHandler()
        {
            if (_eventHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= _eventHandler;
                _eventHandler = null;
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
        internal Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            // Is this our task assembly?
            if (_taskAssemblyFile != null)
            {
                if (FileSystems.Default.FileExists(_taskAssemblyFile))
                {
                    try
                    {
                        AssemblyNameExtension taskAssemblyName = new AssemblyNameExtension(AssemblyName.GetAssemblyName(_taskAssemblyFile));
                        AssemblyNameExtension argAssemblyName = new AssemblyNameExtension(args.Name);

                        if (taskAssemblyName.Equals(argAssemblyName))
                        {
#if (!CLR2COMPATIBILITY)
                            return Assembly.UnsafeLoadFrom(_taskAssemblyFile);
#else
                            return Assembly.LoadFrom(_taskAssemblyFile);
#endif

                        }
                    }
                    // any problems with the task assembly? return null.
                    catch (FileNotFoundException)
                    {
                        return null;
                    }
                    catch (BadImageFormatException)
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


        // we have to store the event handler instance in case we have to remove it
        private ResolveEventHandler _eventHandler = null;
        // path to the task assembly, but only if it's loaded using LoadFrom. If it's loaded with Load, this is null.
        private string _taskAssemblyFile = null;
    }
}
#endif
