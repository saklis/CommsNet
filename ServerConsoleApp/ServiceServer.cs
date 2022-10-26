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
        public async Task<int> TestRemoteCall(string title, string desc, int count, Transmission transmission = null) {
            Console.WriteLine($"Client {transmission.SessionIdentity} called TestRemoteCall with arguments:");
            Console.WriteLine($"\ttitle: {title} (Type: {title.GetType()})");
            Console.WriteLine($"\tdesc: {desc} (Type: {title.GetType()})");
            Console.WriteLine($"\tcount: {count} (Type: {title.GetType()})");

            return 42;
        }
    }
}