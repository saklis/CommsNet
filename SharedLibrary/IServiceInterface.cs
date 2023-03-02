using System;
using System.Threading.Tasks;
using CommsNet.Structures;
using SharedLibrary.DataContainers;

namespace SharedLibrary {
    public interface IServiceInterface {
        /// <summary>
        ///     Example of method with return type (DateeTime) and without any parameters
        /// </summary>
        /// <param name="transmission">Required parameter to give access to transmission properties on the receiving end</param>
        /// <returns>Value returned by remote client.</returns>
        Task<DateTime> GetDateAsync(Transmission transmission = null);

        /// <summary>
        ///     Example of method without return type and without any parameters.
        /// </summary>
        /// <param name="transmission">Required parameter to give access to transmission properties on the receiving end</param>
        Task PingAsync(Transmission transmission = null);

        /// <summary>
        ///     Example of method with return type (Guid) and two parameters (string, string).
        /// </summary>
        /// <param name="login">First parameter.</param>
        /// <param name="password">Second parameter.</param>
        /// <param name="transmission">Required parameter to give access to transmission properties on the receiving end</param>
        /// <returns>Guid returned by the remote client. Guid.Empty when login failed.</returns>
        Task<Guid> LoginUserAsync(string login, string password, Transmission transmission = null);

        /// <summary>
        ///     Example of method with custom return type (HelloResponse) and custom argument (HelloRequest).
        /// </summary>
        /// <param name="message">First parameter.</param>
        /// <param name="transmission">Required parameter to give access to transmission properties on the receiving end</param>
        /// <returns>Object created by the remote client.</returns>
        Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null);
    }
}