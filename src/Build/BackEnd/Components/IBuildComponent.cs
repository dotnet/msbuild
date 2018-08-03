// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Each component in the build system which is registered with the Build Manager or a Node must
    /// implement this interface.
    /// 
    /// REFACTOR: Maybe these could all implement IDisposable.
    /// </summary>
    internal interface IBuildComponent
    {
        /// <summary>
        /// Called by the build component host when a component is first initialized
        /// </summary>
        /// <param name="host">The host for the component.</param>
        void InitializeComponent(IBuildComponentHost host);

        /// <summary>
        /// Called by the build component host when the component host is about to shutdown
        /// </summary>
        void ShutdownComponent();
    }
}
