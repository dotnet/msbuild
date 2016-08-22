using System;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ProjectJsonMigration
{
  public class MigrationTrace
  {
    public static MigrationTrace Instance { get; set; }

    static MigrationTrace ()
    {
      Instance = new MigrationTrace();
    }

    public string EnableEnvironmentVariable => "DOTNET_MIGRATION_TRACE";

    public bool IsEnabled
    {
      get
      {
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable(EnableEnvironmentVariable) != null;
#endif
      }
    }

    public void Write(string message)
    {
      if (IsEnabled)
      {
        Console.Write(message);
      }
    }

    public void WriteLine(string message)
    {
      if (IsEnabled)
      {
        Console.WriteLine(message);
      }
    }
  }
}