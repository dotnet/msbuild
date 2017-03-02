//-----------------------------------------------------------------------
// <copyright file="BuildManagerContainerConfiguration.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Configuration for generating Test Extension Container.</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;

    /// <summary>
    /// An enumeration of all component types recognized by the system    
    /// </summary>
    public enum ComponentType
    {
        /// <summary>
        /// Request Manager component type.
        /// </summary>
        RequestManager,

        /// <summary>
        /// Scheduler component type.
        /// </summary>
        Scheduler,

        /// <summary>
        /// Results Cache component type.
        /// </summary>
        ResultsCache,

        /// <summary>
        /// Property Cache component type.
        /// </summary>
        PropertyCache,

        /// <summary>
        /// The Build Request Configuration Cache component type.
        /// </summary>
        ConfigCache,

        /// <summary>
        /// Node Manager component type.
        /// </summary>
        NodeManager,

        /// <summary>
        /// InProcNodeProvider component type.
        /// </summary>
        InProcNodeProvider,

        /// <summary>
        /// OutOfProcNodeProvider component type.
        /// </summary>
        OutOfProcNodeProvider,

        /// <summary>
        /// RemoteNodeProvider component type.
        /// </summary>
        RemoteNodeProvider,

        /// <summary>
        /// Node packet factory component type.
        /// </summary>
        NodePacketFactory,

        /// <summary>
        /// Request engine component type.
        /// </summary>
        RequestEngine,

        /// <summary>
        /// File monitor component type.
        /// </summary>
        FileMonitor,

        /// <summary>
        /// The endpoint on a Node component type.
        /// </summary>
        NodeEndpoint,

        /// <summary>
        /// The logging service component type.
        /// </summary>
        LoggingService,

        /// <summary>
        /// The component responsible for building requests.
        /// </summary>
        RequestBuilder,

        /// <summary>
        /// The component responsible for building targets.       
        /// </summary>
        TargetBuilder,

        /// <summary>
        /// The component responsible for building tasks.
        /// </summary>
        TaskBuilder,

        /// <summary>
        /// The component which holds all of the building projects.
        /// </summary>
        ProjectCollection,

        /// <summary>
        /// The component which is responsible for providing test data to the variour components.
        /// </summary>
        TestDataProvider
    }

    /// <summary>
    /// Provides configuration information on how the Test Extension Container should be generated.
    /// </summary>
    public class BuildManagerContainerConfiguration : ContainerGeneratorConfiguration, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the BuildManagerContainerConfiguration class.
        /// </summary>
        public BuildManagerContainerConfiguration()
        {
            this.ComponentsToMock = new Dictionary<ComponentType, string>();
            this.TestExtensionForComponents = new Dictionary<ComponentType, string>();
        }

        /// <summary>
        /// Gets Default configuration just registers the BuildManagerTestExtenison.
        /// </summary>
        public static BuildManagerContainerConfiguration Default
        {
            get
            {
                BuildManagerContainerConfiguration configuration = new BuildManagerContainerConfiguration();

                // Attach a test extension for a default component.
                configuration.TestExtensionForComponents.Add(ComponentType.ConfigCache, "Microsoft.Build.ApexTests.Library.ConfigurationCacheTestExtension");
                configuration.TestExtensionForComponents.Add(ComponentType.ResultsCache, "Microsoft.Build.ApexTests.Library.ResultsCacheTestExtension");
                return configuration;
            }
        }

        /// <summary>
        /// Gets Build components which should be replaced by the mock version of the components. The mock class name should be specified.
        /// </summary>
        public Dictionary<ComponentType, string> ComponentsToMock
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets Build components which should be aggregated by test extensions. The test extension class name should be specified.
        /// </summary>
        public Dictionary<ComponentType, string> TestExtensionForComponents
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets Generator type.
        /// </summary>
        public override Type GeneratorType
        {
            get
            {
                return typeof(BuildManagerContainerGenerator);
            }
        }

        /// <summary>
        /// Cleanup any resources created by this object.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleanup any resources created by this object.
        /// </summary>
        /// <param name="disposing">If we are in the process of disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.TestExtensionForComponents.Clear();
                this.ComponentsToMock.Clear();
            }
        }
    }
}