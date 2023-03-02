using System;
using System.Threading.Tasks;
using CommsNet;
using CommsNet.Structures;
using SharedLibrary;
using SharedLibrary.DataContainers;

namespace ServerConsoleApp; 

public class ServiceServer : ServiceManager, IServiceInterface {
    public async Task<DateTime> GetDateAsync(Transmission transmission = null) {
        DateTime retVal = DateTime.Now;

        Console.WriteLine($"Client {transmission.SessionIdentity.ToString()} called GetDate(). Returned value: {retVal.ToString()}");
        return retVal;
    }

    public async Task PingAsync(Transmission transmission = null) {
        Console.WriteLine($"Client {transmission.SessionIdentity.ToString()} pinged!");
    }

    public async Task<Guid> LoginUserAsync(string login, string password, Transmission transmission = null) {
        // randomly do a login
        if (new Random().Next(0, 2) == 1) {
            Guid newGuid = Guid.NewGuid();
            Console.WriteLine($"Client {transmission.SessionIdentity.ToString()} called LoginUser(). Login success! Returned: {newGuid.ToString()}");
            return newGuid;
        }

        Console.WriteLine($"Client {transmission.SessionIdentity.ToString()} called LoginUser(). Login failed! Returned: null");
        return Guid.Empty;
    }

    public async Task<HelloResponse> SayHello(HelloRequest message, Transmission transmission = null) {
        Console.WriteLine($"Client {transmission.SessionIdentity.ToString()} called SayHello.");
        Console.WriteLine($"Message: {message.Greetings}. Float sent: {message.SomeFloat.ToString()}");
        return new HelloResponse {
                                     Reply = $"Hey! Very nice {message.SomeFloat.ToString()} float!"
                                 };
    }
}