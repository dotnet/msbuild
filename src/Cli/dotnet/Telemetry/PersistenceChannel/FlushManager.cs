// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using IChannelTelemetry = Microsoft.ApplicationInsights.Channel.ITelemetry;

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel
{
    /// <summary>
    ///     This class handles all the logic for flushing the In Memory buffer to the persistent storage.
    /// </summary>
    internal class FlushManager
    {
        /// <summary>
        ///     The storage that is used to persist all the transmissions.
        /// </summary>
        private readonly BaseStorageService _storage;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FlushManager" /> class.
        /// </summary>
        /// <param name="storage">The storage that persists the telemetries.</param>
        internal FlushManager(BaseStorageService storage)
        {
            _storage = storage;
        }

        /// <summary>
        ///     Gets or sets the service endpoint.
        /// </summary>
        /// <remarks>
        ///     Q: Why flushManager knows about the endpoint?
        ///     A: Storage stores Transmission objects and Transmission objects contain the endpoint address.
        /// </remarks>
        internal Uri EndpointAddress { get; set; }


        /// <summary>
        ///     Persist the in-memory telemetry items.
        /// </summary>
        internal void Flush(IChannelTelemetry telemetryItem)
        {
            if (telemetryItem != null)
            {
                byte[] data = JsonSerializer.Serialize(new[] { telemetryItem });
                Transmission transmission = new(
                    EndpointAddress,
                    data,
                    "application/x-json-stream",
                    JsonSerializer.CompressionType);

                _storage.EnqueueAsync(transmission).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
    }
}
