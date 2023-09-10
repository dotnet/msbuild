// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.HotReload;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class UpdatePayloadTests
    {
        [Fact]
        public async Task UpdatePayload_CanRoundTrip()
        {
            var initial = new UpdatePayload(
                new[]
                {
                    new UpdateDelta(
                        moduleId: Guid.NewGuid(),
                        ilDelta: new byte[] { 0, 0, 1 },
                        metadataDelta: new byte[] { 0, 1, 1 },
                        updatedTypes: Array.Empty<int>()),
                    new UpdateDelta(
                        moduleId: Guid.NewGuid(),
                        ilDelta: new byte[] { 1, 0, 0 },
                        metadataDelta: new byte[] { 1, 0, 1 },
                        updatedTypes: Array.Empty<int>())
                });

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        [Fact]
        public async Task UpdatePayload_CanRoundTripUpdatedTypes()
        {
            var initial = new UpdatePayload(
                new[]
                {
                    new UpdateDelta(
                        moduleId: Guid.NewGuid(),
                        ilDelta: new byte[] { 0, 0, 1 },
                        metadataDelta: new byte[] { 0, 1, 1 },
                        updatedTypes: new int[] { 60, 74, 22323 }),
                    new UpdateDelta(
                        moduleId: Guid.NewGuid(),
                        ilDelta: new byte[] { 1, 0, 0 },
                        metadataDelta: new byte[] { 1, 0, 1 },
                        updatedTypes: new int[] { -18 })
                });

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        [Fact]
        public async Task UpdatePayload_WithLargeDeltas_CanRoundtrip()
        {
            var initial = new UpdatePayload(
                new[]
                {
                    new UpdateDelta(
                        moduleId: Guid.NewGuid(),
                        ilDelta: Enumerable.Range(0, 68200).Select(c => (byte)(c%2)).ToArray(),
                        metadataDelta: new byte[] { 0, 1, 1 },
                        updatedTypes: Array.Empty<int>())
                });

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        private static void AssertEqual(UpdatePayload initial, UpdatePayload read)
        {
            Assert.Equal(initial.Deltas.Count, read.Deltas.Count);

            for (var i = 0; i < initial.Deltas.Count; i++)
            {
                var e = initial.Deltas[i];
                var a = read.Deltas[i];

                Assert.Equal(e.ModuleId, a.ModuleId);
                Assert.Equal(e.ILDelta, a.ILDelta);
                Assert.Equal(e.MetadataDelta, a.MetadataDelta);
                if (e.UpdatedTypes is null)
                {
                    Assert.Empty(a.UpdatedTypes);
                }
                else
                {
                    Assert.Equal(e.UpdatedTypes, a.UpdatedTypes);
                }
            }
        }
    }
}
