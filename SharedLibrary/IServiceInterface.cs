using CommsNet.Structures;
using System;
using System.Threading.Tasks;
using SharedLibrary.DataContainers;

namespace SharedLibrary
{
    public interface IServiceInterface {
        Task<int> TestRemoteCall(string title, string desc, int count, Transmission transmission = null);
    }
}