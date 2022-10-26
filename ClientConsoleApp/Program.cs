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
            client.ConnectionClosedRemotely += identity => {
                                                   Console.WriteLine();
                                                   Console.WriteLine($"Server closed connection.");
                                               };

            client.ConnectToServer("localhost", 12345, 12346);
            
            Console.WriteLine("Connected!");

            try {
                // test RemoteCallAsync method
                WaitForKey("Press any key to test RemoteCall...");
                
                Console.Write("Calling TestRemoteCall... ");
                int result = await client.TestRemoteCall("foo", "bar", 24);
                Console.WriteLine("Success!");
                Console.WriteLine($"Result of RemoteCall test is: {result}");
                
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

        private static void WaitForKey(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey(true);
        }
    }
}