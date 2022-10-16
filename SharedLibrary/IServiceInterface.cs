using CommsNet.Structures;
using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace SharedLibrary
{
    public interface IServiceInterface
    {
        /// <summary>
        ///     Example of method with return value (DateTime) and without parameter.
        /// </summary>
        Task<DateTime> GetDate(Transmission transmission = null);

        /// <summary>
        ///     Example of method without return value and without parameter.
        /// </summary>
        Task Ping(Transmission transmission = null);

        /// <summary>
        ///     Example of method with return value (HelloResponse) and with parameter (HelloRequest).
        /// </summary>
        Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null);

        /// <summary>
        ///     Example of method without return value and with parameter (int).
        /// </summary>
        Task SendAge(int age, Transmission transmission = null);

        /// <summary>
        ///     Example of method with parameter and with return value using simple types.
        /// </summary>
        Task<bool> IsOfAge(int age, Transmission transmission = null);
    }
}