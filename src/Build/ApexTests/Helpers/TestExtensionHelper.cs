//-----------------------------------------------------------------------
// <copyright file="TestExtensionHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Test extension verifier for the BuildManager implementation</summary>
//-----------------------------------------------------------------------
namespace Microsoft.Build.ApexTests.Library
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using Microsoft.Test.Apex;

    /// <summary>
    /// Helper class to create new Test Extensions for extensions which derive of TestExtension
    /// </summary>
    public static class TestExtensionHelper
    {
        /// <summary>
        /// Creates a new test extension.
        /// </summary>
        /// <typeparam name="TTestExtension">Type of test extension.</typeparam>
        /// <typeparam name="TType">Type the test extension is for.</typeparam>
        /// <param name="type">Object which is passed to the test extension constructor.</param>
        /// <param name="currentTestExtension">TestExtension from where this is called.</param>
        /// <returns>Test extension created.</returns>
        public static TTestExtension Create<TTestExtension, TType>(TType type, TestExtension currentTestExtension)
            where TType : class
            where TTestExtension : TestExtension
        {
            Type testExtensionType = typeof(TTestExtension);
            object[] parameters = { type };
            TTestExtension extension = (TTestExtension)testExtensionType.InvokeMember(null, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance, null, null, parameters, CultureInfo.InvariantCulture);
            LifeTimeManagmentServiceTestExtension lifeTimeManagmentService = currentTestExtension.Container.GetFirstTestExtension<LifeTimeManagmentServiceTestExtension>();

            // This is a temporary workaround to get any test extension to be able to get the lifetime managment service till we implement our own test extension factory.
            if (extension.Container == null)
            {
                extension.Container = currentTestExtension.Container;
            }

            lifeTimeManagmentService.AddToCompositionContainer(extension);
    
            return extension;
        }
    }
}