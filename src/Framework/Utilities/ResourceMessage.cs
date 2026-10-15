// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Represents a resource name and deferred formatting args.
/// </summary>
/// <typeparam name="TArgs">The deferred arg provider type.</typeparam>
/// <param name="resourceName">The resource name.</param>
/// <param name="args">The deferred formatting args.</param>
internal readonly struct ResourceMessage<TArgs>(string resourceName, TArgs args)
    where TArgs : struct, IMessageArgs
{
    /// <summary>
    ///  Gets the resource name.
    /// </summary>
    /// <value>
    ///  The resource name.
    /// </value>
    public string ResourceName => resourceName;

    /// <summary>
    ///  Gets the deferred formatting args.
    /// </summary>
    /// <value>
    ///  The deferred formatting args.
    /// </value>
    public TArgs Args => args;

    /// <summary>
    ///  Formats the deferred args into a composite format string.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <returns>
    ///  The formatted string.
    /// </returns>
    public string Format([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        => args.Format(format);
}

/// <summary>
///  Creates MSBuild resource messages and adds deferred formatting args.
/// </summary>
internal static class MSBuildResources
{
    /// <summary>
    ///  Creates a resource message without formatting args.
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <returns>
    ///  The resource message.
    /// </returns>
    public static ResourceMessage<MessageArgs> MSBuildResource(string resourceName)
        => new(resourceName, default);

    /// <summary>
    ///  Adds deferred formatting args to an MSBuild resource message.
    /// </summary>
    /// <param name="resource">The resource message.</param>
    extension(ResourceMessage<MessageArgs> resource)
    {
        /// <summary>
        ///  Adds one deferred arg to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1>> WithArgs<T1>(T1 arg1)
            => new(resource.ResourceName, MessageArgs.Create(arg1));

        /// <summary>
        ///  Adds two deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2>> WithArgs<T1, T2>(T1 arg1, T2 arg2)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2));

        /// <summary>
        ///  Adds three deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3>> WithArgs<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3));

        /// <summary>
        ///  Adds four deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <typeparam name="T4">The type of the fourth arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3, T4>> WithArgs<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3, arg4));

        /// <summary>
        ///  Adds five deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <typeparam name="T4">The type of the fourth arg.</typeparam>
        /// <typeparam name="T5">The type of the fifth arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <param name="arg5">The fifth arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3, T4, T5>> WithArgs<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3, arg4, arg5));

        /// <summary>
        ///  Adds six deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <typeparam name="T4">The type of the fourth arg.</typeparam>
        /// <typeparam name="T5">The type of the fifth arg.</typeparam>
        /// <typeparam name="T6">The type of the sixth arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <param name="arg5">The fifth arg.</param>
        /// <param name="arg6">The sixth arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3, T4, T5, T6>> WithArgs<T1, T2, T3, T4, T5, T6>(
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3, arg4, arg5, arg6));

        /// <summary>
        ///  Adds seven deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <typeparam name="T4">The type of the fourth arg.</typeparam>
        /// <typeparam name="T5">The type of the fifth arg.</typeparam>
        /// <typeparam name="T6">The type of the sixth arg.</typeparam>
        /// <typeparam name="T7">The type of the seventh arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <param name="arg5">The fifth arg.</param>
        /// <param name="arg6">The sixth arg.</param>
        /// <param name="arg7">The seventh arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3, T4, T5, T6, T7>> WithArgs<T1, T2, T3, T4, T5, T6, T7>(
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3, arg4, arg5, arg6, arg7));

        /// <summary>
        ///  Adds eight deferred args to the resource message.
        /// </summary>
        /// <typeparam name="T1">The type of the first arg.</typeparam>
        /// <typeparam name="T2">The type of the second arg.</typeparam>
        /// <typeparam name="T3">The type of the third arg.</typeparam>
        /// <typeparam name="T4">The type of the fourth arg.</typeparam>
        /// <typeparam name="T5">The type of the fifth arg.</typeparam>
        /// <typeparam name="T6">The type of the sixth arg.</typeparam>
        /// <typeparam name="T7">The type of the seventh arg.</typeparam>
        /// <typeparam name="T8">The type of the eighth arg.</typeparam>
        /// <param name="arg1">The first arg.</param>
        /// <param name="arg2">The second arg.</param>
        /// <param name="arg3">The third arg.</param>
        /// <param name="arg4">The fourth arg.</param>
        /// <param name="arg5">The fifth arg.</param>
        /// <param name="arg6">The sixth arg.</param>
        /// <param name="arg7">The seventh arg.</param>
        /// <param name="arg8">The eighth arg.</param>
        /// <returns>
        ///  The resource message with deferred args.
        /// </returns>
        public ResourceMessage<MessageArgs<T1, T2, T3, T4, T5, T6, T7, T8>> WithArgs<T1, T2, T3, T4, T5, T6, T7, T8>(
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
            => new(resource.ResourceName, MessageArgs.Create(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
    }
}
