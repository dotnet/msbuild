using FluentAssertions;
using Microsoft.Net.Sdk.WorkloadManifestReader;
using System.IO;
using Xunit;

namespace ManifestReaderTests
{
    public class ManifestTests
    {

        [Fact]
        public void ItCanDeserialize()
        {
            using (FileStream fsSource = new FileStream(Path.Combine("Manifests", "Sample.json"), FileMode.Open, FileAccess.Read))
            {
                var result = WorkloadManifestReader.ReadWorkloadManifest(fsSource);
                result.Version.Should().Be(5);

                result.Packs["Xamarin.Android.Sdk"].Id.Should().Be("Xamarin.Android.Sdk");
                result.Packs["Xamarin.Android.Sdk"].IsAlias.Should().Be(false);
                result.Packs["Xamarin.Android.Sdk"].Kind.Should().Be(WorkloadPackKind.Sdk);
                result.Packs["Xamarin.Android.Sdk"].Version.Should().Be("8.4.7");
            }
        }
    }
}
