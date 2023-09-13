using System.Diagnostics;
using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(UpdateHandler))]

// delete the dependency dll to cause load failure of DepSubType
var depPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location!)!, "Dep.dll");
File.Delete(depPath);
Console.WriteLine($"File deleted: {depPath}");

while (true)
{
    lock (UpdateHandler.Guard)
    {
        Printer.Print();
    }

    Thread.Sleep(100);
}

static class UpdateHandler
{
    // Lock to avoid the updated Print method executing concurrently with the update handler.
    public static object Guard = new object();

    public static void UpdateApplication(Type[] types)
    {
        lock (Guard)
        {
            Console.WriteLine($"Updated types: {(types == null ? "<null>" : types.Length == 0 ? "<empty>" : string.Join(",", types.Select(t => t.Name)))}");
        }
    }
}
