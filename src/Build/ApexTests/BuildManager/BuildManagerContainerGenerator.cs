//-----------------------------------------------------------------------
// <copyright file="BuildManagerContainerGenerator.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Container containing the Test Extensions.</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.ComponentModel.Composition;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using Microsoft.Build.BackEnd;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Test.Apex;
    using Microsoft.Test.Apex.Services;

    /// <summary>
    /// Responsible for creating a Test Extension Container which hosts the Test Extensions required
    /// for testing.
    /// </summary>
    public class BuildManagerContainerGenerator : ContainerGenerator<BuildManagerContainerConfiguration>
    {
        /// <summary>
        /// Gets or sets the LifetimeService for factoried extensions to be added to. 
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "The setter is invoked by binding of a component domain.")]
        [Import]
        public IFactoryProductActivatorService LifetimeService
        {
            get;
            set;
        }

        /// <summary>
        /// This method is passed as a delegate to the BuildManager - so when the BuildManager needs to create an instance of this component this method is invoked.
        /// </summary>
        /// <param name="buildComponentType">Mock object to instantiate.</param>
        /// <returns>IBuildComponent of the Mock.</returns>
        internal IBuildComponent CreateMockComponent(BuildComponentType buildComponentType)
        {
            string typeToMock = buildComponentType.ToString();
            if (String.IsNullOrEmpty(typeToMock))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "No mocks are available for {0} build component type.", buildComponentType.ToString()));
            }

            ComponentType componentType = StringToEnum<ComponentType>(typeToMock);
            string typeToMockName = this.Configuration.ComponentsToMock[componentType];
            Type mockType = Assembly.GetAssembly(typeof(BuildManagerContainerGenerator)).GetType(typeToMockName, true, true);
            return (IBuildComponent)mockType.InvokeMember(null, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance, null, null, new object[] { }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Responsible for creating the build manager.
        /// For MSBuild backend testing BuildManager is the Entry point for all tests. That is - the environment
        /// for testing starts with the BuildManager. Configuration can specify which components should be mocked
        /// and which test extension should be attached to which component.
        /// </summary>
        /// <returns>TextExtensions created.</returns>
        protected override TestExtensionContainer Generate()
        {
            List<TestExtension> testExtensions = new List<TestExtension>();

            // To workaround the problem where extensions derived out from Testextension does not have a LifeTimeManagmentService.
            LifeTimeManagmentServiceTestExtension lifetimeServiceExtension = new LifeTimeManagmentServiceTestExtension(LifetimeService);
            LifetimeService.Compose(lifetimeServiceExtension);
            testExtensions.Add(lifetimeServiceExtension);

            // Create the build manager and the associated test extension first.
            BuildManagerTestExtension buildManagerTestExtension = new BuildManagerTestExtension(BuildManager.DefaultBuildManager);
            LifetimeService.Compose(buildManagerTestExtension);
            testExtensions.Add(buildManagerTestExtension);

            // When the BuildManager is created it registers a default set of components.
            // Loop through each of the components that we want to mock and then replace the component in the BuildManager.
            foreach (KeyValuePair<ComponentType, string> componentTypePair in this.Configuration.ComponentsToMock)
            {
                buildManagerTestExtension.ReplaceRegisterdFactory(GetBuildComponentTypeFromComponentType(componentTypePair.Key.ToString()), this.CreateMockComponent);
            }

            // Loop through each of the components that we want to wrap with a test extension - create the test extension and aggregate the internal component.
            // This component could be a mock that we create above or the real implementation.
            foreach (KeyValuePair<ComponentType, string> componentTypePair in this.Configuration.TestExtensionForComponents)
            {
                TestExtension extension = CreateTestExtensionForComponent(componentTypePair.Key.ToString(), componentTypePair.Value, buildManagerTestExtension);
                LifetimeService.Compose(extension);
                testExtensions.Add(extension);
            }

            TestExtensionContainer testContainer = new TestExtensionContainer(testExtensions);
            return testContainer;
        }

        /// <summary>
        /// Create a TestExtension for a given component type.
        /// </summary>
        /// <param name="componentName">Name of the component for which the TestExtension is to be created.</param>
        /// <param name="testExtensionName">Fully qualified TestExtension name.</param>
        /// <param name="buildManagerTestExtension">BuildManager Test extension entry.</param>
        /// <returns>New TestExtension.</returns>
        private static TestExtension CreateTestExtensionForComponent(string componentName, string testExtensionName, BuildManagerTestExtension buildManagerTestExtension)
        {
            BuildComponentType type = StringToEnum<BuildComponentType>(componentName);
            IBuildComponent component = buildManagerTestExtension.GetComponent(type);
            Type testExtensionType = Assembly.GetAssembly(typeof(BuildManagerContainerGenerator)).GetType(testExtensionName, true, true);
            object[] parameters = { component };
            return (TestExtension)testExtensionType.InvokeMember(null, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance, null, null, parameters, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts the ComponentType to a BuildComponentType.
        /// </summary>
        /// <param name="componentTypeString">ComponentType string.</param>
        /// <returns>BuildComponentType for the requested string.</returns>
        private static BuildComponentType GetBuildComponentTypeFromComponentType(string componentTypeString)
        {
            try
            {
                BuildComponentType component = StringToEnum<BuildComponentType>(componentTypeString);
                return component;
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "Component type: {0} is not a valid MSBuild build component type.", componentTypeString));
            }
        }

        /// <summary>
        /// Helper method to convert string to enum.
        /// </summary>
        /// <typeparam name="T">Enum to get.</typeparam>
        /// <param name="name">String value of enum.</param>
        /// <returns>Enum type of requested string.</returns>
        private static T StringToEnum<T>(string name)
        {
            return (T)Enum.Parse(typeof(T), name);
        }
    }
}
