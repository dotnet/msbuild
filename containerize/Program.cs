using System.CommandLine;
using System.Containers;

RootCommand rootCommand = new("Containerize an application without Docker.");

Option<DirectoryInfo> fileOption = new(
    name: "--folder",
    description: "The folder to pack.");

rootCommand.AddOption(fileOption);

Option<string> containerPath = new(
    name: "--containerPath",
    description: "Location of the packed folder in the final image.");

rootCommand.AddOption(containerPath);

Option<string> registryUri = new(
    name: "--registry",
    description: "Location of the registry to push to.");

rootCommand.AddOption(registryUri);

Option<string> baseImageName = new(
    name: "--base",
    description: "Base image name.");

rootCommand.AddOption(baseImageName);

Option<string> baseImageTag = new(
    name: "--baseTag",
    description: "Base image tag.");

rootCommand.AddOption(baseImageTag);

Option<string> entrypoint = new(
    name: "--entrypoint",
    description: "Entrypoint application.");

rootCommand.AddOption(entrypoint);

Option<string> imageName = new(
    name: "--name",
    description: "Name of the new image.");

rootCommand.AddOption(imageName);

rootCommand.SetHandler(async (folder, cp, uri, baseImageName, baseTag, entrypoint, imageName) =>
{
    await Containerize(folder.FullName, cp, uri, baseImageName, baseTag, entrypoint, imageName);
},
    fileOption,
    containerPath,
    registryUri,
    baseImageName,
    baseImageTag, 
    entrypoint,
    imageName);

return await rootCommand.InvokeAsync(args);

async Task Containerize(string folder, string containerPath, string registryName, string baseName, string baseTag, string entrypoint, string imageName)
{
    Registry registry = new Registry(new Uri($"http://{registryName}"));

    Image x = await registry.GetImageManifest(baseName, baseTag);

    Layer l = Layer.FromDirectory(folder, "/app");

    x.AddLayer(l);

    x.SetEntrypoint(entrypoint);

    // Push the image back to the local registry

    await registry.Push(x, imageName, baseName);

    Console.WriteLine($"Pushed {registryName}/{imageName}:latest");
}