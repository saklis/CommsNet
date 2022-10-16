using CommsNet;
using CommsNet.Structures;
using SharedLibrary;
using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace ServerConsoleApp
{
    public class ServiceServer : ServiceManager, IServiceInterface
    {
        /// <summary>
        ///     Example of method with return value (DateTime) and without parameter.
        /// </summary>
        public async Task<DateTime> GetDate(Transmission transmission = null)
        {
            Console.WriteLine($"Client {transmission.SessionIdentity} called GetDate.");
            return DateTime.Now;
        }

        /// <summary>
        ///     Example of method without return value and without parameter.
        /// </summary>
        public async Task Ping(Transmission transmission = null)
        {
            Console.WriteLine($"Client {transmission.SessionIdentity} pinged!");
        }

        /// <summary>
        ///     Example of method with return value (HelloResponse) and with parameter (HelloRequest).
        /// </summary>
        public async Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null)
        {
            Console.WriteLine($"Client {transmission.SessionIdentity} called SayHello.");
            Console.WriteLine($"Message: {message.Greetings}. Float sent: {message.SomeFloat}");
            return new HelloResponse()
            {
                Reply = $"Hey! Very nice {message.SomeFloat} float!"
            };
        }

        /// <summary>
        ///     Example of method without return value and with parameter (int).
        /// </summary>
        public async Task SendAge(int age, Transmission transmission = null)
        {
            Console.WriteLine($"Client {transmission.SessionIdentity} called SendAge.");
            Console.WriteLine($"Client's age is {age}.");
        }

        /// <summary>
        ///     Example of method with parameter and with return value using simple types.
        /// </summary>
        public async Task<bool> IsOfAge(int age, Transmission transmission = null)
        {
            return age >= 18;
        }
    }
}