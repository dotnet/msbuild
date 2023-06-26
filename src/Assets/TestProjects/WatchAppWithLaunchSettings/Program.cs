Console.WriteLine("Started");
Console.WriteLine($"Environment: {Environment.GetEnvironmentVariable("EnvironmentFromProfile")}");
Console.WriteLine($"Arguments: {string.Join(",", args)}");

if (Environment.GetEnvironmentVariable("READ_INPUT") != null)
{
    var read = Console.ReadLine();
    Console.WriteLine("Echo: " + read);
}