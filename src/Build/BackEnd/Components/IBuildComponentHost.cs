// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BuildParameters = Microsoft.Build.Execution.BuildParameters;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Factory delegate which instantiates a component of the type specified.
    /// </summary>
    /// <param name="type">The type of component to be instantiated.</param>
    /// <returns>An instance of the component.</returns>
    internal delegate IBuildComponent BuildComponentFactoryDelegate(BuildComponentType type);

    /// <summary>
    /// An enumeration of all component types recognized by the system
    /// </summary>
    internal enum BuildComponentType
    {
        /// <summary>
        /// Request Manager
        /// </summary>
        RequestManager,

        /// <summary>
        /// Scheduler
        /// </summary>
        Scheduler,

        /// <summary>
        /// Results Cache
        /// </summary>
        ResultsCache,

        /// <summary>
        /// Property Cache
        /// </summary>
        PropertyCache,

        /// <summary>
        /// The Build Request Configuration Cache
        /// </summary>
        ConfigCache,

        /// <summary>
        /// Node Manager
        /// </summary>
        NodeManager,

        /// <summary>
        /// InProcNodeProvider
        /// </summary>
        InProcNodeProvider,

        /// <summary>
        /// OutOfProcNodeProvider
        /// </summary>
        OutOfProcNodeProvider,

        /// <summary>
        /// RemoteNodeProvider
        /// </summary>
        RemoteNodeProvider,

        /// <summary>
        /// Node packet factory
        /// </summary>
        NodePacketFactory,

        /// <summary>
        /// Request engine
        /// </summary>
        RequestEngine,

        /// <summary>
        /// File monitor
        /// </summary>
        FileMonitor,

        /// <summary>
        /// The endpoint on a Node
        /// </summary>
        NodeEndpoint,

        /// <summary>
        /// The logging service
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
        /// The component which is responsible for providing test data to the variour components
        /// </summary>
        TestDataProvider,

        /// <summary>
        /// OutOfProcTaskHostNodeProvider
        /// </summary>
        OutOfProcTaskHostNodeProvider,

        /// <summary>
        /// Node manager for task host nodes
        /// </summary>
        TaskHostNodeManager,

        /// <summary>
        /// The cache of registered disposable objects.
        /// </summary>
        RegisteredTaskObjectCache,

        /// <summary>
        /// The SDK resolution service.
        /// </summary>
        SdkResolverService,

#if FEATURE_REPORTFILEACCESSES
        /// <summary>
        /// The component which is the sink for file access reports and forwards reports to other components.
        /// </summary>
        FileAccessManager,
#endif

        /// <summary>
        /// The component which launches new MSBuild nodes.
        /// </summary>
        NodeLauncher,

        /// <summary>
        /// The Build Check Manager.
        /// </summary>
        BuildCheckManagerProvider,

        /// <summary>
        /// The component which collects telemetry data in worker node and forwards it to the main node.
        /// </summary>
        TelemetryForwarder,
    }

    /// <summary>
    /// This interface is implemented by objects which host build components.
    /// </summary>
    internal interface IBuildComponentHost
    {
        #region Methods

        /// <summary>
        /// Retrieves the name of the host.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Retrieves the BuildParameters used during the build.
        /// </summary>
        BuildParameters BuildParameters { get; }

        /// <summary>
        /// The data structure which holds the data for the use of legacy threading semantics
        /// </summary>
        LegacyThreadingData LegacyThreadingData { get; }

        /// <summary>
        /// Retrieves the logging service associated with a particular build
        /// </summary>
        ILoggingService LoggingService { get; }

        /// <summary>
        /// Registers a factory which will be used to create the necessary components of the build
        /// system.
        /// </summary>
        /// <param name="factoryType">The type which is created by this factory.</param>
        /// <param name="factory">The factory to be registered.</param>
        /// <remarks>
        /// It is not necessary to register any factories.  If no factory is registered for a specific kind
        /// of object, the system will use the default factory.
        /// </remarks>
        void RegisterFactory(BuildComponentType factoryType, BuildComponentFactoryDelegate factory);

        /// <summary>
        /// Gets an instance of the specified component type from the host.
        /// </summary>
        /// <param name="type">The component type to be retrieved</param>
        /// <returns>The component</returns>
        IBuildComponent GetComponent(BuildComponentType type);

        /// <summary>
        /// Gets an instance of the specified component type from the host.
        /// </summary>
        /// <typeparam name="TComponent"></typeparam>
        /// <param name="type">The component type to be retrieved</param>
        /// <returns>The component</returns>
        TComponent GetComponent<TComponent>(BuildComponentType type)
            where TComponent : IBuildComponent;

        #endregion
    }
}
