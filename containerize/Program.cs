using System.CommandLine;
using System.Containers;

var fileOption = new Argument<DirectoryInfo>(
    name: "folder",
    description: "The folder to pack.")
    .LegalFilePathsOnly().ExistingOnly();

Option<string> registryUri = new(
    name: "--registry",
    description: "Location of the registry to push to.",
    getDefaultValue: () => "localhost:5010");

Option<string> baseImageName = new(
    name: "--base",
    description: "Base image name.",
    getDefaultValue: () => "dotnet/runtime");

Option<string> baseImageTag = new(
    name: "--baseTag",
    description: "Base image tag.",
    getDefaultValue: () => $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription[5]}.0");

Option<string> entrypoint = new(
    name: "--entrypoint",
    description: "Entrypoint application.");

Option<string> imageName = new(
    name: "--name",
    description: "Name of the new image.");

var imageTag = new Option<string>("--tag", description: "Tag of the new image.", getDefaultValue: () => "latest");

var workingDir = new Option<string>("--working-dir", description: "The working directory of the application", getDefaultValue: () => "/app");

RootCommand rootCommand = new("Containerize an application without Docker."){
    fileOption,
    registryUri,
    baseImageName,
    baseImageTag,
    entrypoint,
    imageName,
    imageTag,
    workingDir
};
rootCommand.SetHandler(async (folder, containerWorkingDir, uri, baseImageName, baseTag, entrypoint, imageName, imageTag) =>
{
    await Containerize(folder, containerWorkingDir, uri, baseImageName, baseTag, entrypoint, imageName, imageTag);
},
    fileOption,
    workingDir,
    registryUri,
    baseImageName,
    baseImageTag, 
    entrypoint,
    imageName,
    imageTag
    );

return await rootCommand.InvokeAsync(args);

async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string entrypoint, string imageName, string imageTag)
{
    Registry registry = new Registry(new Uri($"http://{registryName}"));

    Console.WriteLine($"Reading from {registry.BaseUri}");

    Image x = await registry.GetImageManifest(baseName, baseTag);
    x.WorkingDirectory = workingDir;

    Console.WriteLine($"Copying from {folder.FullName} to {workingDir}");
    Layer l = Layer.FromDirectory(folder.FullName, workingDir);

    x.AddLayer(l);

    x.SetEntrypoint(entrypoint);

    // Push the image back to the local registry

    await registry.Push(x, imageName, baseName);

    Console.WriteLine($"Pushed {registryName}/{imageName}:latest");

    var pullBase = System.Diagnostics.Process.Start("docker", $"pull {registryName}/{imageName}:latest");
    await pullBase.WaitForExitAsync();

    Console.WriteLine($"Loaded image into local Docker daemon. Use 'docker run --rm -it --name {imageName} {registryName}/{imageName}:latest' to run the application.");

}