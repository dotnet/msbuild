//-----------------------------------------------------------------------
// <copyright file="LifeTimeManagmentServiceTestExtension.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension Apex LifeTimeManagmentService implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using Microsoft.Test.Apex;
    using Microsoft.Test.Apex.Services;

    /// <summary>
    /// Temporary workaround to pass the Apex LifeTimeManagmentService so that extensions derived from TestExtension can be registered with the domain.
    /// </summary>
    public class LifeTimeManagmentServiceTestExtension : TestExtension<LifeTimeManagmentServiceVerifier>
    {
        /// <summary>
        /// Initializes a new instance of the LifeTimeManagmentServiceTestExtension class.
        /// </summary>
        /// <param name="lifetimeManagementService">Instance Apex life time managment service.</param>
        internal LifeTimeManagmentServiceTestExtension(IFactoryProductActivatorService lifetimeManagementService)
            : base()
        {
            this.LifetimeManagementService = lifetimeManagementService;
        }

        /// <summary>
        /// Gets or sets the instance Apex life time managment service.
        /// </summary>
        internal IFactoryProductActivatorService LifetimeManagementService
        {
            get;
            private set;
        }

        /// <summary>
        /// Add a component to the lifetime management domain.
        /// </summary>
        /// <param name="instance">
        /// The object instance to be placed in application domain and bound.
        /// </param>
        public void AddToCompositionContainer(object instance)
        {
            this.LifetimeManagementService.Compose(instance);
        }
    }
}