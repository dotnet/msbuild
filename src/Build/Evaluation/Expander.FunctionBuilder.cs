// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private struct FunctionBuilder<T>
        where T : class, IProperty
    {
        /// <summary>
        /// The type of this function's receiver.
        /// </summary>
        public Type ReceiverType { get; set; }

        /// <summary>
        /// The name of the function.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The arguments for the function.
        /// </summary>
        public string[] Arguments { get; set; }

        /// <summary>
        /// The expression that this function is part of.
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// The property name that this function is applied on.
        /// </summary>
        public string Receiver { get; set; }

        /// <summary>
        /// The binding flags that will be used during invocation of this function.
        /// </summary>
        public BindingFlags BindingFlags { get; set; }

        /// <summary>
        /// The remainder of the body once the function and arguments have been extracted.
        /// </summary>
        public string Remainder { get; set; }

        public IFileSystem FileSystem { get; set; }

        public LoggingContext LoggingContext { get; set; }

        /// <summary>
        /// List of properties which have been used but have not been initialized yet.
        /// </summary>
        public PropertiesUseTracker PropertiesUseTracker { get; set; }

        internal readonly Function<T> Build()
        {
            return new Function<T>(
                ReceiverType,
                Expression,
                Receiver,
                Name,
                Arguments,
                BindingFlags,
                Remainder,
                PropertiesUseTracker,
                FileSystem,
                LoggingContext);
        }
    }
}
