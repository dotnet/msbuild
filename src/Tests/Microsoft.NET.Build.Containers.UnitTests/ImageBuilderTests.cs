// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ImageBuilderTests
{
    private readonly TestLoggerFactory _loggerFactory;

    public ImageBuilderTests(ITestOutputHelper output)
    {
        _loggerFactory = new TestLoggerFactory(output);
    }

    [Fact]
    public void CanAddLabelsToImage()
    {
        string simpleImageConfig =
            """
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "AttachStdin": false,
                      "AttachStdout": false,
                      "AttachStderr": false,
                      "Tty": false,
                      "OpenStdin": false,
                      "StdinOnce": false,
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "Volumes": null,
                      "WorkingDir": "",
                      "Entrypoint": null,
                      "OnBuild": null,
                      "Labels": null
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

        JsonNode? node = JsonNode.Parse(simpleImageConfig);
        Assert.NotNull(node);

        ImageConfig baseConfig = new ImageConfig(node);

        baseConfig.AddLabel("testLabel1", "v1");
        baseConfig.AddLabel("testLabel2", "v2");

        string readyImage = baseConfig.BuildConfig();

        JsonNode? result = JsonNode.Parse(readyImage);

        var resultLabels = result?["config"]?["Labels"] as JsonObject;
        Assert.NotNull(resultLabels);

        Assert.Equal(2, resultLabels.Count);
        Assert.Equal("v1", resultLabels["testLabel1"]?.ToString());
        Assert.Equal("v2", resultLabels["testLabel2"]?.ToString());
    }

    [Fact]
    public void CanPreserveExistingLabels()
    {
        string simpleImageConfig =
            """
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "AttachStdin": false,
                      "AttachStdout": false,
                      "AttachStderr": false,
                      "Tty": false,
                      "OpenStdin": false,
                      "StdinOnce": false,
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "Volumes": null,
                      "WorkingDir": "",
                      "Entrypoint": null,
                      "OnBuild": null,
                      "Labels":
                      {
                        "existing" : "e1",
                        "existing2" : "e2"
                      }
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

        JsonNode? node = JsonNode.Parse(simpleImageConfig);
        Assert.NotNull(node);

        ImageConfig baseConfig = new ImageConfig(node);

        baseConfig.AddLabel("testLabel1", "v1");
        baseConfig.AddLabel("existing2", "v2");

        string readyImage = baseConfig.BuildConfig();

        JsonNode? result = JsonNode.Parse(readyImage);

        var resultLabels = result?["config"]?["Labels"] as JsonObject;
        Assert.NotNull(resultLabels);

        Assert.Equal(3, resultLabels.Count);
        Assert.Equal("v1", resultLabels["testLabel1"]?.ToString());
        Assert.Equal("v2", resultLabels["existing2"]?.ToString());
        Assert.Equal("e1", resultLabels["existing"]?.ToString());
    }

    [Fact]
    public void CanAddPortsToImage()
    {
        string simpleImageConfig =
            """
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "AttachStdin": false,
                      "AttachStdout": false,
                      "AttachStderr": false,
                      "Tty": false,
                      "OpenStdin": false,
                      "StdinOnce": false,
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "Volumes": null,
                      "WorkingDir": "",
                      "Entrypoint": null,
                      "OnBuild": null,
                      "Labels": null
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

        JsonNode? node = JsonNode.Parse(simpleImageConfig);
        Assert.NotNull(node);

        ImageConfig baseConfig = new ImageConfig(node);

        baseConfig.ExposePort(6000, PortType.tcp);
        baseConfig.ExposePort(6010, PortType.udp);

        string readyImage = baseConfig.BuildConfig();

        JsonNode? result = JsonNode.Parse(readyImage);

        var resultPorts = result?["config"]?["ExposedPorts"] as JsonObject;
        Assert.NotNull(resultPorts);

        Assert.Equal(2, resultPorts.Count);
        Assert.NotNull(resultPorts["6000/tcp"] as JsonObject);
        Assert.NotNull( resultPorts["6010/udp"] as JsonObject);
    }

    [Fact]
    public void CanPreserveExistingPorts()
    {
        string simpleImageConfig =
            """
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "AttachStdin": false,
                      "AttachStdout": false,
                      "AttachStderr": false,
                      "Tty": false,
                      "OpenStdin": false,
                      "StdinOnce": false,
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "Volumes": null,
                      "WorkingDir": "",
                      "Entrypoint": null,
                      "OnBuild": null,
                      "Labels": null,
                      "ExposedPorts":
                      {
                        "6100/tcp": {},
                        "6200": {}
                      }
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

        JsonNode? node = JsonNode.Parse(simpleImageConfig);
        Assert.NotNull(node);

        ImageConfig baseConfig = new ImageConfig(node);

        baseConfig.ExposePort(6000, PortType.tcp);
        baseConfig.ExposePort(6010, PortType.udp);
        baseConfig.ExposePort(6100, PortType.udp);
        baseConfig.ExposePort(6200, PortType.tcp);

        string readyImage = baseConfig.BuildConfig();

        JsonNode? result = JsonNode.Parse(readyImage);

        var resultPorts = result?["config"]?["ExposedPorts"] as JsonObject;
        Assert.NotNull(resultPorts);

        Assert.Equal(5, resultPorts.Count);
        Assert.NotNull(resultPorts["6000/tcp"] as JsonObject);
        Assert.NotNull(resultPorts["6010/udp"] as JsonObject);
        Assert.NotNull(resultPorts["6100/udp"] as JsonObject);
        Assert.NotNull(resultPorts["6100/tcp"] as JsonObject);
        Assert.NotNull(resultPorts["6200/tcp"] as JsonObject);
    }

    [Fact]
    public void HistoryEntriesMatchNonEmptyLayers()
    {
        // Note how the base image config is already "corrupt" by having
        // only 5 layers with only 2 history entries. We expect the
        // final config to also have 5 (non empty) history entries.

        string simpleImageConfig =
            """
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "AttachStdin": false,
                      "AttachStdout": false,
                      "AttachStderr": false,
                      "Tty": false,
                      "OpenStdin": false,
                      "StdinOnce": false,
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "ASPNETCORE_URLS=http://+:80",
                        "DOTNET_RUNNING_IN_CONTAINER=true",
                        "DOTNET_VERSION=7.0.2",
                        "ASPNET_VERSION=7.0.2"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "Volumes": null,
                      "WorkingDir": "",
                      "Entrypoint": null,
                      "OnBuild": null,
                      "Labels": null,
                      "ExposedPorts":
                      {
                        "6100/tcp": {},
                        "6200": {}
                      }
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
                    },
                    "history": [{
                        "created": "2023-03-01T04:09:58.669479866Z",
                        "created_by": "/bin/sh -c #(nop) ADD file:493a5b0c8d2d63a1343258b3f9aa5fcd59a93f44fe26ad9e56b094c3a08fd3be in / "
                    }, {
                        "created": "2023-03-01T04:09:59.032972609Z",
                        "created_by": "/bin/sh -c #(nop)  CMD [\u0022bash\u0022]",
                        "empty_layer": true
                    }]
                }
                """;

        JsonNode? node = JsonNode.Parse(simpleImageConfig);
        Assert.NotNull(node);

        ImageConfig baseConfig = new(node);

        string readyImage = baseConfig.BuildConfig();

        JsonNode? result = JsonNode.Parse(readyImage);

        var historyNode = result?["history"];
        Assert.NotNull(historyNode);

        var layerDiffsNode = result?["rootfs"]?["diff_ids"];
        Assert.NotNull(layerDiffsNode);

        int nonEmptyHistoryNodes = historyNode.AsArray()
            .Count(h => h?.AsObject()["empty_layer"]?.GetValue<bool>() is null or false);
        int layerCount = layerDiffsNode.AsArray().Count;
        Assert.Equal(nonEmptyHistoryNodes, layerCount);
    }


    [Fact]
    public void CanSetUserFromAppUIDEnvVarFromBaseImage()
    {
        var expectedUid = "12345";
        var builder = FromBaseImageConfig($$"""
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                        "{{ImageBuilder.EnvironmentVariables.APP_UID}}={{expectedUid}}"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "WorkingDir": ""
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
                """);

        var builtImage = builder.Build();

        JsonNode? result = JsonNode.Parse(builtImage.Config);
        Assert.NotNull(result);
        var assignedUid = result["config"]?["User"]?.GetValue<string>();
        Assert.Equal(assignedUid, expectedUid);
    }

    [Fact]
    public void CanSetUserFromAppUIDEnvVarFromUser()
    {
        var expectedUid = "12345";
        var builder = FromBaseImageConfig($$"""
                {
                    "architecture": "amd64",
                    "config": {
                      "Hostname": "",
                      "Domainname": "",
                      "User": "",
                      "Env": [
                        "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
                      ],
                      "Cmd": ["bash"],
                      "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                      "WorkingDir": ""
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
                """);

        builder.AddEnvironmentVariable(ImageBuilder.EnvironmentVariables.APP_UID, "12345");
        var builtImage = builder.Build();

        JsonNode? result = JsonNode.Parse(builtImage.Config);
        Assert.NotNull(result);
        var assignedUser = result["config"]?["User"]?.GetValue<string>();
        Assert.Equal(assignedUser, expectedUid);
    }

    [InlineData("ASPNETCORE_URLS", "https://*:12345;http://+:1234;http://localhost:123;http://1.2.3.4:12", 12345, 1234, 123, 12)]
    [InlineData("ASPNETCORE_HTTP_PORTS", "999;666", 999, 666)]
    [InlineData("ASPNETCORE_HTTPS_PORTS", "456;789", 456, 789)]
    [Theory]
    public void CanSetPortFromEnvVarFromBaseImage(string envVar, string envValue, params int[] expectedPorts)
    {
        var builder = FromBaseImageConfig($$"""
        {
            "architecture": "amd64",
            "config": {
                "Hostname": "",
                "Domainname": "",
                "User": "",
                "Env": [
                "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                "{{envVar}}={{envValue}}"
                ],
                "Cmd": ["bash"],
                "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                "WorkingDir": ""
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
        """);

        var builtImage = builder.Build();

        JsonNode? result = JsonNode.Parse(builtImage.Config);
        Assert.NotNull(result);
        var portsObject = result["config"]?["ExposedPorts"]?.AsObject();
        var assignedPorts = portsObject?.AsEnumerable().Select(portString => int.Parse(portString.Key.Split('/')[0])).ToArray();
        Assert.Equal(assignedPorts, expectedPorts);
    }

    [InlineData("ASPNETCORE_URLS", "https://*:12345;http://+:1234;http://localhost:123;http://1.2.3.4:12", 12345, 1234, 123, 12)]
    [InlineData("ASPNETCORE_HTTP_PORTS", "999;666", 999, 666)]
    [InlineData("ASPNETCORE_HTTPS_PORTS", "456;789", 456, 789)]
    [Theory]
    public void CanSetPortFromEnvVarFromUser(string envVar, string envValue, params int[] expectedPorts)
    {
        var builder = FromBaseImageConfig($$"""
        {
            "architecture": "amd64",
            "config": {
                "Hostname": "",
                "Domainname": "",
                "User": "",
                "Env": [
                "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
                ],
                "Cmd": ["bash"],
                "Image": "sha256:d772d27ebeec80393349a4770dc37f977be2c776a01c88b624d43f93fa369d69",
                "WorkingDir": ""
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
        """);

        builder.AddEnvironmentVariable(envVar, envValue);

        var builtImage = builder.Build();

        JsonNode? result = JsonNode.Parse(builtImage.Config);
        Assert.NotNull(result);
        var portsObject = result["config"]?["ExposedPorts"]?.AsObject();
        var assignedPorts = portsObject?.AsEnumerable().Select(portString => int.Parse(portString.Key.Split('/')[0])).ToArray();
        Assert.Equal(assignedPorts, expectedPorts);
    }

    private ImageBuilder FromBaseImageConfig(string baseImageConfig, [CallerMemberName] string testName = "")
    {
        var manifest = new ManifestV2() {
            SchemaVersion = 2,
            MediaType = SchemaTypes.DockerManifestV2,
            Config = new ManifestConfig() {
                mediaType = "",
                size = 0,
                digest = "sha256:0"
            },
            Layers = new List<ManifestLayer>()
        };
        return new ImageBuilder(manifest, new ImageConfig(baseImageConfig), _loggerFactory.CreateLogger(testName));
    }
}
