// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  The value-independent result of parsing a property function body such as
///  <c>[MSBuild]::EnsureTrailingSlash($(Dir))</c> or <c>SomeProperty.ToLower()</c>.
/// </summary>
/// <remarks>
///  Parsing a property function body is a pure function of the body text and the receiver's
///  runtime type: it resolves the receiver type, method name, unexpanded argument strings and
///  binding flags, none of which depend on the values those arguments will later expand to.
///  Argument expansion and invocation happen later, in <c>Function.Execute</c>, against a
///  per-call context. Separating the two lets the parse result be shared while each expansion
///  still gets its own mutable <c>Function</c>.
/// </remarks>
internal sealed class PropertyFunctionDescriptor
{
    /// <summary>
    ///  Backing field for <see cref="ReceiverType"/>, annotated so the annotation flows to
    ///  <c>Function</c>'s constructor rather than producing an IL2072 at the hand-off.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields)]
    private readonly Type _receiverType;

    internal PropertyFunctionDescriptor(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)] Type receiverType,
        string expression,
        string? receiver,
        string methodName,
        string[]? arguments,
        BindingFlags bindingFlags,
        string? remainder)
    {
        _receiverType = receiverType;
        Expression = expression;
        Receiver = receiver;
        MethodName = methodName;
        Arguments = arguments;
        BindingFlags = bindingFlags;
        Remainder = remainder;
    }

    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields)]
    internal Type ReceiverType => _receiverType;

    internal string Expression { get; }

    internal string? Receiver { get; }

    internal string MethodName { get; }

    /// <summary>
    ///  The unexpanded argument strings. Read-only in practice: <c>Function.Execute</c> only
    ///  reads this array and writes the expanded values into a fresh array of its own.
    /// </summary>
    internal string[]? Arguments { get; }

    internal BindingFlags BindingFlags { get; }

    internal string? Remainder { get; }
}

/// <summary>
///  Caches <see cref="PropertyFunctionDescriptor"/> instances so that a given property function
///  body is parsed and bound once per receiver type rather than on every expansion.
/// </summary>
/// <remarks>
///  This mirrors the long-standing parsed-expression-tree cache in <see cref="ConditionEvaluator"/>,
///  including its size-capped flush policy: the hit rate is very high in normal builds, and the
///  cost of repopulating after a flush is small compared to the cost of an unbounded cache in
///  pathological cases (for example randomly generated configuration names in VS stress runs).
/// </remarks>
internal static class PropertyFunctionDescriptorCache
{
    /// <summary>
    ///  Matches the threshold used for cached condition expression trees.
    /// </summary>
    private const int CacheSizeThreshold = 3000;

    private static volatile ConcurrentDictionary<Key, PropertyFunctionDescriptor?> s_cache = new();

    /// <summary>
    ///  Approximate entry count, maintained separately because
    ///  <see cref="ConcurrentDictionary{TKey, TValue}.Count"/> acquires every bucket lock and
    ///  would turn the flush check into a contention point under multithreaded evaluation.
    ///  Same approach as <c>ConditionEvaluator</c>'s <c>OptimisticSize</c>.
    /// </summary>
    private static int s_optimisticSize;

    /// <summary>
    ///  A parse is fully determined by the body text plus the receiver's runtime type. The type
    ///  must be part of the key because it selects the parse branch: a <see langword="null"/>
    ///  receiver means a static call or a chain root, an <see cref="Array"/> receiver selects
    ///  <c>GetValue</c> for an indexer, a <see cref="string"/> receiver selects <c>get_Chars</c>,
    ///  and anything else selects <c>get_Item</c>.
    /// </summary>
    private readonly struct Key : IEquatable<Key>
    {
        private readonly string _body;
        private readonly Type? _receiverType;

        internal Key(string body, Type? receiverType)
        {
            _body = body;
            _receiverType = receiverType;
        }

        public bool Equals(Key other)
            => _receiverType == other._receiverType && string.Equals(_body, other._body, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            int hash = StringComparer.Ordinal.GetHashCode(_body);
            return _receiverType is null ? hash : (hash * 397) ^ _receiverType.GetHashCode();
        }
    }

    internal static bool TryGet(string body, Type? receiverType, out PropertyFunctionDescriptor? descriptor)
        => s_cache.TryGetValue(new Key(body, receiverType), out descriptor);

    internal static void Add(string body, Type? receiverType, PropertyFunctionDescriptor? descriptor)
    {
        if (s_optimisticSize > CacheSizeThreshold)
        {
            Clear();
        }

        Interlocked.Increment(ref s_optimisticSize);
        s_cache[new Key(body, receiverType)] = descriptor;
    }

    /// <summary>
    ///  Drops every cached parse. Must be called whenever the environment backing
    ///  <c>FeatureSwitches.EnableAllPropertyFunctions</c> may have changed, because that switch
    ///  decides whether a type outside the curated allowlist is resolvable, and it is read live
    ///  on every access. A reusable MSBuild Server node serves successive build requests with
    ///  different environments, so without this a descriptor parsed while the switch was on
    ///  would still be served after it was turned off.
    /// </summary>
    internal static void Clear()
    {
        s_cache = new ConcurrentDictionary<Key, PropertyFunctionDescriptor?>();
        Interlocked.Exchange(ref s_optimisticSize, 0);
    }
}
