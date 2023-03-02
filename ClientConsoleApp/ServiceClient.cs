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
        public async Task<DateTime> GetDateAsync(Transmission transmission = null) {
            return await RemoteCallAsync<DateTime>(default, nameof(GetDateAsync));
        }

        public async Task PingAsync(Transmission transmission = null) {
            await RemoteCallAsync(default, nameof(PingAsync));
        }

        public async Task<Guid> LoginUserAsync(string login, string password, Transmission transmission = null) {
            return await RemoteCallAsync<Guid>(default, nameof(LoginUserAsync), login, password);
        }

        public async Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null) {
            return await RemoteCallAsync<HelloResponse>(default, nameof(SayHello), message);
        }
    }
}