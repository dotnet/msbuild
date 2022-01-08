Console.WriteLine("Started");
Console.WriteLine($"Environment: {Environment.GetEnvironmentVariable("EnvironmentFromProfile")}");

if (Environment.GetEnvironmentVariable("READ_INPUT") != null)
{
    var read = Console.ReadLine();
    Console.WriteLine("Echo: " + read);
}