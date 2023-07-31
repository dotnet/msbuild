// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ImageConfigTests
{
    private const string SampleImageConfig = """
                {
                    "architecture": "amd64",
                    "config": {
                      "User": "app",
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Volumes": {
                        "/var/log/": {}
                      },
                      "WorkingDir": "/app",
                      "Entrypoint": null,
                      "Labels": null,
                      "StopSignal": "SIGTERM"
                    },
                    "created": "2023-02-04T08:14:52.000901321Z",
                    "os": "linux",
                    "rootfs": {
                      "type": "layers",
                      "diff_ids": [
                        "sha256:bd2fe8b74db65d82ea10db97368d35b92998d4ea0e7e7dc819481fe4a68f64cf",
                        "sha256:94100d1041b650c6f7d7848c550cd98c25d0bdc193d30692e5ea5474d7b3b085",
                        "sha256:53c2a75a33c8f971b4b5036d34764373e134f91ee01d8053b4c3573c42e1cf5d",
                        "sha256:49a61320e585180286535a2545be5722b09e40ad44c7c190b20ec96c9e42e4a3",
                        "sha256:8a379cce2ac272aa71aa029a7bbba85c852ba81711d9f90afaefd3bf5036dc48"
                      ]
                    }
                }
                """;

    [InlineData("User")]
    [InlineData("Volumes")]
    [InlineData("StopSignal")]
    [Theory]
    public void PassesThroughPropertyEvenThoughPropertyIsntExplicitlyHandled(string property)
    {
        ImageConfig c = new(SampleImageConfig);
        JsonNode after = JsonNode.Parse(c.BuildConfig())!;
        JsonNode? prop = after["config"]?[property];
        Assert.NotNull(prop);
    }
}
