using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace ClientConsoleApp;

internal class Program {
    private static async Task Main(string[] args) {
        Console.WriteLine("Welcome in CommsNet Client ConsoleApp!");

        // connecting to service
        WaitForKey("Press any key to connect to service...");

        Console.Write("Connecting to server... ");

        ServiceClient client = new();
        client.SessionEncounteredError += (identity, exception) => {
                                              Console.WriteLine();
                                              Console.WriteLine("Exception in CommsNet encountered!");
                                              Console.WriteLine(exception);
                                          };
        client.ConnectionClosedRemotely += identity => {
                                               Console.WriteLine();
                                               Console.WriteLine("Server closed connection.");
                                           };

        client.ConnectToServer("localhost", 12345, 12346);

        Console.WriteLine("Connected!");

        try {
            // test RemoteCallAsync method
            WaitForKey("Press any key to test remote calls...");

            Console.Write("Calling Ping()... ");
            await client.PingAsync();
            Console.WriteLine("Success!");

            Console.Write("Calling GetDate()...");
            DateTime remoteTime = await client.GetDateAsync();
            Console.WriteLine($"Success! Returned time: {remoteTime.ToString()}");

            Console.Write("Calling LoginUser()...");
            Guid? token = await client.LoginUserAsync("userLogin", "userPassword");
            Console.WriteLine($"Success! User logged in? {(token == Guid.Empty ? "NO!" : $"YES! Token:{token.ToString()}")}");

            Console.Write("Calling SayHello()...");
            HelloResponse response = await client.SayHello(new HelloRequest { Greetings = "Hello, friend!", SomeFloat = 42 });
            Console.WriteLine($"Success! Remote client replied with '{response.Reply}'");

            Console.Write("Calling Ping() again... ");
            await client.PingAsync();
            Console.WriteLine("Success!");

            // close connection
            WaitForKey("Press any key to close connection...");

            Console.Write("Closing connection...");
            client.DisconnectFromServer();
        } catch (Exception e) {
            Console.WriteLine();
            Console.WriteLine(e);
        } finally {
            Console.WriteLine();
            WaitForKey("Press any key to close console...");
        }
    }

    private static void WaitForKey(string message) {
        Console.WriteLine(message);
        Console.ReadKey(true);
    }
}