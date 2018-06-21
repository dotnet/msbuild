// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Helper class for maintaining the component collection
    /// </summary>
    internal class BuildComponentFactoryCollection
    {
        /// <summary>
        /// The build component factories.
        /// </summary>
        private readonly Dictionary<BuildComponentType, BuildComponentEntry> _componentEntriesByType;

        /// <summary>
        /// The host used to initialize components.
        /// </summary>
        private readonly IBuildComponentHost _host;

        /// <summary>
        /// Constructor.
        /// </summary>
        public BuildComponentFactoryCollection(IBuildComponentHost host)
        {
            _host = host;
            _componentEntriesByType = new Dictionary<BuildComponentType, BuildComponentEntry>();
        }

        /// <summary>
        /// The creation pattern to use for this component.
        /// </summary>
        public enum CreationPattern
        {
            /// <summary>
            /// The component should be created as a singleton.
            /// </summary>
            Singleton,

            /// <summary>
            /// A new instance of the component should be created with every request.
            /// </summary>
            CreateAlways
        }

        /// <summary>
        /// Registers the default factories.
        /// </summary>
        public void RegisterDefaultFactories()
        {
            _componentEntriesByType[BuildComponentType.Scheduler] = new BuildComponentEntry(BuildComponentType.Scheduler, Scheduler.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.ConfigCache] = new BuildComponentEntry(BuildComponentType.ConfigCache, ConfigCache.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.ResultsCache] = new BuildComponentEntry(BuildComponentType.ResultsCache, ResultsCache.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.NodeManager] = new BuildComponentEntry(BuildComponentType.NodeManager, NodeManager.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.TaskHostNodeManager] = new BuildComponentEntry(BuildComponentType.TaskHostNodeManager, TaskHostNodeManager.CreateComponent, CreationPattern.Singleton);

            _componentEntriesByType[BuildComponentType.InProcNodeProvider] = new BuildComponentEntry(BuildComponentType.InProcNodeProvider, NodeProviderInProc.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.OutOfProcNodeProvider] = new BuildComponentEntry(BuildComponentType.OutOfProcNodeProvider, NodeProviderOutOfProc.CreateComponent, CreationPattern.Singleton);
            _componentEntriesByType[BuildComponentType.OutOfProcTaskHostNodeProvider] = new BuildComponentEntry(BuildComponentType.OutOfProcTaskHostNodeProvider, NodeProviderOutOfProcTaskHost.CreateComponent, CreationPattern.Singleton);

            // PropertyCache,
            // RemoteNodeProvider,
            // NodePacketFactory,
            _componentEntriesByType[BuildComponentType.RequestEngine] = new BuildComponentEntry(BuildComponentType.RequestEngine, BuildRequestEngine.CreateComponent, CreationPattern.Singleton);

            // FileMonitor,
            // NodeEndpoint,
            _componentEntriesByType[BuildComponentType.LoggingService] = new BuildComponentEntry(BuildComponentType.LoggingService, null);
            _componentEntriesByType[BuildComponentType.RequestBuilder] = new BuildComponentEntry(BuildComponentType.RequestBuilder, RequestBuilder.CreateComponent, CreationPattern.CreateAlways);
            _componentEntriesByType[BuildComponentType.TargetBuilder] = new BuildComponentEntry(BuildComponentType.TargetBuilder, TargetBuilder.CreateComponent, CreationPattern.CreateAlways);
            _componentEntriesByType[BuildComponentType.TaskBuilder] = new BuildComponentEntry(BuildComponentType.TaskBuilder, TaskBuilder.CreateComponent, CreationPattern.CreateAlways);
            _componentEntriesByType[BuildComponentType.RegisteredTaskObjectCache] = new BuildComponentEntry(BuildComponentType.RegisteredTaskObjectCache, RegisteredTaskObjectCache.CreateComponent, CreationPattern.Singleton);

            // SDK resolution
            _componentEntriesByType[BuildComponentType.SdkResolverService] = new BuildComponentEntry(BuildComponentType.SdkResolverService, MainNodeSdkResolverService.CreateComponent, CreationPattern.Singleton);
        }

        /// <summary>
        /// Shuts down all factories registered to this component factory collection. 
        /// </summary>
        public void ShutdownComponents()
        {
            foreach (KeyValuePair<BuildComponentType, BuildComponentEntry> componentEntry in _componentEntriesByType)
            {
                if (componentEntry.Value.Pattern == CreationPattern.Singleton)
                {
                    componentEntry.Value.ShutdownSingletonInstance();
                }
            }
        }

        /// <summary>
        /// Shuts down a specific singleton component.
        /// </summary>
        public void ShutdownComponent(BuildComponentType componentType)
        {
            BuildComponentEntry existingEntry = _componentEntriesByType[componentType];
            existingEntry.ShutdownSingletonInstance();
        }

        /// <summary>
        /// Registers a factory to replace one of the defaults.  Creation pattern is inherited from the original.
        /// </summary>
        /// <param name="componentType">The type which is created by this factory.</param>
        /// <param name="factory">The factory to be registered.</param>
        public void ReplaceFactory(BuildComponentType componentType, BuildComponentFactoryDelegate factory)
        {
            BuildComponentEntry existingEntry = _componentEntriesByType[componentType];
            _componentEntriesByType[componentType] = new BuildComponentEntry(componentType, factory, existingEntry.Pattern);
        }

        /// <summary>
        /// Registers a factory to replace one of the defaults.  Creation pattern is inherited from the original.
        /// </summary>
        /// <param name="componentType">The type which is created by this factory.</param>
        /// <param name="instance">The instance to be registered.</param>
        public void ReplaceFactory(BuildComponentType componentType, IBuildComponent instance)
        {
            ErrorUtilities.VerifyThrow(_componentEntriesByType[componentType].Pattern == CreationPattern.Singleton, "Previously existing factory for type {0} was not a singleton factory.", componentType);
            _componentEntriesByType[componentType] = new BuildComponentEntry(componentType, instance);
        }

        /// <summary>
        /// Adds a factory.
        /// </summary>
        /// <param name="componentType">The type which is created by this factory.</param>
        /// <param name="factory">Delegate which is responsible for creating the Component.</param>
        /// <param name="creationPattern">Creation pattern.</param>
        public void AddFactory(BuildComponentType componentType, BuildComponentFactoryDelegate factory, CreationPattern creationPattern)
        {
            _componentEntriesByType[componentType] = new BuildComponentEntry(componentType, factory, creationPattern);
        }

        /// <summary>
        /// Gets an instance of the specified component type from the host.
        /// </summary>
        /// <param name="type">The component type to be retrieved</param>
        /// <returns>The component</returns>
        public IBuildComponent GetComponent(BuildComponentType type)
        {
            if (!_componentEntriesByType.TryGetValue(type, out BuildComponentEntry componentEntry))
            {
                ErrorUtilities.ThrowInternalError("No factory registered for component type {0}", type);
            }

            return componentEntry.GetInstance(_host);
        }

        /// <summary>
        /// A helper class wrapping build components.
        /// </summary>
        private class BuildComponentEntry
        {
            /// <summary>
            /// The factory used to construct instances of the component.
            /// </summary>
            private readonly BuildComponentFactoryDelegate _factory;

            /// <summary>
            /// The singleton instance for components which adhere to the singleton pattern.
            /// </summary>
            private IBuildComponent _singleton;

            /// <summary>
            /// Constructor.
            /// </summary>
            public BuildComponentEntry(BuildComponentType type, BuildComponentFactoryDelegate factory, CreationPattern pattern)
            {
                ComponentType = type;
                _factory = factory;
                Pattern = pattern;
            }

            /// <summary>
            /// Constructor for existing singleton.
            /// </summary>
            public BuildComponentEntry(BuildComponentType type, IBuildComponent singleton)
            {
                ComponentType = type;
                _singleton = singleton;
                Pattern = CreationPattern.Singleton;
            }

            /// <summary>
            /// Retrieves the component type.
            /// </summary>
            private BuildComponentType ComponentType { get; }

            /// <summary>
            /// Retrieves the creation pattern.
            /// </summary>
            public CreationPattern Pattern { get; }

            /// <summary>
            /// Gets an instance of the component.
            /// </summary>
            public IBuildComponent GetInstance(IBuildComponentHost host)
            {
                if (Pattern == CreationPattern.Singleton)
                {
                    if (_singleton == null)
                    {
                        _singleton = _factory(ComponentType);
                        _singleton.InitializeComponent(host);
                    }

                    return _singleton;
                }

                IBuildComponent component = _factory(ComponentType);
                component.InitializeComponent(host);
                return component;
            }

            /// <summary>
            /// Shuts down the single instance for this component type.
            /// </summary>
            public void ShutdownSingletonInstance()
            {
                ErrorUtilities.VerifyThrow(Pattern == CreationPattern.Singleton, "Cannot shutdown non-singleton.");
                if (_singleton != null)
                {
                    _singleton.ShutdownComponent();
                    _singleton = null;
                }
            }
        }
    }
}
