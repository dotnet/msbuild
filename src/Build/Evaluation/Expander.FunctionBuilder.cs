// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private struct FunctionBuilder
    {
        /// <summary>
        /// Backing field for <see cref="ReceiverType"/>. Carries the same annotation as the property so the
        /// getter's return value is satisfied by a field with a matching requirement: a compiler-generated
        /// auto-property backing field does not inherit the property's annotation, which is what produces IL2078.
        /// </summary>
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
        private Type _receiverType;

        /// <summary>
        /// The type of this function's receiver. Only the public member surface is preserved for trimming:
        /// property functions never bind non-public members (<see cref="BindingFlags.NonPublic"/> is rejected
        /// by <c>TypeExtensions.InvokePublicMember</c>). Keep in sync with <c>Function._receiverType</c> and
        /// <c>Constants.PropertyFunctionMembers</c>.
        /// </summary>
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
        public readonly Type ReceiverType => _receiverType;

        /// <summary>
        /// Sets <see cref="ReceiverType"/> from a property-function receiver type. That type is always either
        /// a type from MSBuild's curated static-method allowlist (resolved by name, its public members
        /// preserved for trimming by <c>Constants.PropertyFunctionMembers</c>) or a runtime value's
        /// <c>GetType()</c>; property functions bind only the public surface. This one-line setter writes the
        /// annotated <see cref="_receiverType"/> field directly, so it is the single place an un-annotated
        /// <see cref="Type"/> enters the <c>DynamicallyAccessedMembers</c>-tracked flow and the localized,
        /// minimized home of the IL2069 suppression - every downstream hop (<c>Build</c> -> <c>Function</c>
        /// -> <c>InvokePublicMember</c>) is then machine-checked.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2069",
            Justification = "Receiver type comes from the static-method allowlist (public members preserved by Constants.PropertyFunctionMembers) or a runtime GetType(); only public members are bound. See the summary for the DAM-flow rationale.")]
        internal void SetReceiverType(Type receiverType) => _receiverType = receiverType;

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

        internal readonly Function Build()
        {
            return new Function(
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
