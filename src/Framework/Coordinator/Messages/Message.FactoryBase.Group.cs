// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record Message<TMessageType>
{
    protected abstract partial class FactoryBase<TMessage, TFactory>
    {
        protected sealed class Group
        {
            private readonly FrozenDictionary<TMessageType, TFactory> _factories;

            public Group(params ImmutableArray<TFactory> factories)
            {
                var map = new Dictionary<TMessageType, TFactory>(factories.Length);

                foreach (TFactory factory in factories)
                {
                    Assumed.Zero(GetTypeByte(factory.MessageType) & ExtendedBit, $"{typeof(TMessageType).Name} value {factory.MessageType} uses the reserved extended-fields bit.");
                    Assumed.False(map.ContainsKey(factory.MessageType), $"Duplicate factory for {typeof(TMessageType).Name}: {factory.MessageType}");
                    map.Add(factory.MessageType, factory);
                }

                _factories = map.ToFrozenDictionary();
            }

            public TFactory GetFactory(TMessageType messageType)
                => _factories.TryGetValue(messageType, out TFactory? factory)
                    ? factory
                    : Assumed.Unreachable<TFactory>($"Invalid {typeof(TMessageType).Name}: {messageType}");
        }
    }
}
