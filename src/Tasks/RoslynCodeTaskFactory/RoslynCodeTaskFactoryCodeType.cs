// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents the kind of code contained in the code task definition.
    /// </summary>
    internal enum RoslynCodeTaskFactoryCodeType
    {
        /// <summary>
        /// The code is a fragment and should be included within a method.
        /// </summary>
        Fragment,

        /// <summary>
        /// The code is a method and should be included within a class.
        /// </summary>
        Method,

        /// <summary>
        /// The code is a whole class and no modifications should be made to it.
        /// </summary>
        Class,
    }
}
