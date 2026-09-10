// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal sealed class TaskResultCacheStatisticsPacket : INodePacket
    {
        private long _requests;
        private long _hits;
        private long _misses;
        private long _ineligible;
        private long _errors;
        private long _stores;
        private long _storeErrors;
        private long _hitDurationTicks;
        private long _missDurationTicks;

        private TaskResultCacheStatisticsPacket()
        {
        }

        internal TaskResultCacheStatisticsPacket(TaskResultCacheStatisticsSnapshot snapshot)
        {
            _requests = snapshot.Requests;
            _hits = snapshot.Hits;
            _misses = snapshot.Misses;
            _ineligible = snapshot.Ineligible;
            _errors = snapshot.Errors;
            _stores = snapshot.Stores;
            _storeErrors = snapshot.StoreErrors;
            _hitDurationTicks = snapshot.HitDurationTicks;
            _missDurationTicks = snapshot.MissDurationTicks;
        }

        public NodePacketType Type => NodePacketType.TaskResultCacheStatistics;

        internal TaskResultCacheStatisticsSnapshot Snapshot =>
            new(
                _requests,
                _hits,
                _misses,
                _ineligible,
                _errors,
                _stores,
                _storeErrors,
                _hitDurationTicks,
                _missDurationTicks);

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requests);
            translator.Translate(ref _hits);
            translator.Translate(ref _misses);
            translator.Translate(ref _ineligible);
            translator.Translate(ref _errors);
            translator.Translate(ref _stores);
            translator.Translate(ref _storeErrors);
            translator.Translate(ref _hitDurationTicks);
            translator.Translate(ref _missDurationTicks);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskResultCacheStatisticsPacket();
            packet.Translate(translator);
            return packet;
        }
    }
}
