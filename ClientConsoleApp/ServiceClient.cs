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
        public async Task<int> TestRemoteCall(string title, string desc, int count, Transmission transmission = null) {
            return await RemoteCallAsync<int>(default, nameof(TestRemoteCall), title, desc, count);
        }
    }
}