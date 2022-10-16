using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace ClientConsoleApp
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome in CommsNet Client ConsoleApp!");

            // connecting to service
            WaitForKey("Press any key to connect to service...");

            Console.Write("Connecting to server... ");

            ServiceClient client = new ServiceClient();
            client.SessionEncounteredError += (identity, exception) =>
            {
                Console.WriteLine();
                Console.WriteLine("Exception in CommsNet encountered!");
                Console.WriteLine(exception);
            };
            client.ConnectToServer("localhost", 12345, 12346);

            Console.WriteLine("Connected!");

            try
            {
                // call Ping method
                WaitForKey("Press any key to call Ping...");

                Console.Write("Calling Ping... ");
                await client.Ping();
                Console.WriteLine("Success!");

                // call SayHello
                WaitForKey("Press any key to call SayHello...");

                HelloRequest request = new HelloRequest()
                {
                    Greetings = "Hello from the client!",
                    SomeFloat = 42.42f
                };

                Console.Write("Calling SayHello...");
                HelloResponse response = await client.SayHello(request);
                Console.WriteLine($"Success! Response: {response.Reply}");

                // close connection
                WaitForKey("Press any key to close connection...");

                Console.Write("Closing connection...");
                client.DisconnectFromServer();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine();
                WaitForKey("Press any key to close console...");
            }
        }

        private static void WaitForKey(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey(true);
        }
    }
}