using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Net.Sdk.WorkloadManifestReader;
using System.IO;
using Xunit;

namespace ManifestReaderTests
{
    public class ManifestReaderFunctionalTests
    {
        [Fact(Skip = "GetInstalledWorkloadPacksOfKind is not implemented")]
        public void ItCanGetAllTemplatesPacks()
        {
            using FileStream fsSource =
                new FileStream(Path.Combine("Manifests", "Sample.json"), FileMode.Open, FileAccess.Read);
            var workloadResolver = new WorkloadResolver(new FakeManifestProvider(new[] { fsSource }), "fakepath");
            var result = workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Template);
            result.Should().HaveCount(1);
        }

        private class FakeManifestProvider : IWorkloadManifestProvider
        {
            private readonly IEnumerable<Stream> _streams;

            public FakeManifestProvider(IEnumerable<Stream> streams)
            {
                _streams = streams;
            }

            public IEnumerable<Stream> GetManifests()
            {
                return _streams;
            }
        }
    }
}
