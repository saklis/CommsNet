using CommsNet;
using CommsNet.Structures;
using SharedLibrary;
using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace ClientConsoleApp
{
    public class ServiceClient : ServiceManager, IServiceInterface
    {
        /// <summary>
        ///     Example of method with return value (DateTime) and without parameter.
        /// </summary>
        public async Task<DateTime> GetDate(Transmission transmission = null)
        {
            return await ExecuteRemoteAsync<DateTime>(default, nameof(GetDate));
        }

        /// <summary>
        ///     Example of method without return value and without parameter.
        /// </summary>
        public async Task Ping(Transmission transmission = null)
        {
            await ExecuteRemoteAsync(default, nameof(Ping));
        }

        /// <summary>
        ///     Example of method with return value (HelloResponse) and with parameter (HelloRequest).
        /// </summary>
        public async Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null)
        {
            return await ExecuteRemoteAsync<HelloResponse, HelloRequest>(default, nameof(SayHello), message);
        }

        /// <summary>
        ///     Example of method without return value and with parameter (int).
        /// </summary>
        public async Task SendAge(int age, Transmission transmission = null)
        {
            await ExecuteRemoteAsync<int>(default, nameof(SendAge), age);
        }

        /// <summary>
        ///     Example of method with parameter and with return value using simple types.
        /// </summary>
        public async Task<bool> IsOfAge(int age, Transmission transmission = null)
        {
            return await ExecuteRemoteAsync<bool, int>(default, nameof(IsOfAge), age);
        }
    }
}