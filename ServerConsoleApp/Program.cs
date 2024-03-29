﻿using System;

namespace ServerConsoleApp;

internal class Program {
    private static void Main(string[] args) {
        Console.WriteLine("Welcome in CommsNet Server ConsoleApp!");

        // starting server
        WaitForKey("Press any key to start the server...");

        Console.Write("Starting server...");
        ServiceServer server = new();
        server.NewConnectionEstablished += (identity, connection) => Console.WriteLine($"New connection accepted! Identity: {identity.ToString()}");
        server.SessionEncounteredError += (identity, exception) => {
                                              Console.WriteLine();
                                              Console.WriteLine($"Exception in CommsNet encountered! Session: {identity.ToString()}");
                                              Console.WriteLine(exception);
                                          };
        server.ConnectionClosedRemotely += identity => {
                                               Console.WriteLine();
                                               Console.WriteLine($"Client {identity.ToString()} closed connection from its side.");
                                           };
        server.StartServer(12345);
        Console.WriteLine("Success!");

        try {
            // wait for client calls

            WaitForKey("Press any key to close server...");

            Console.Write("Stopping server...");
            server.StopServer();
            Console.WriteLine("Success!");
        } catch (Exception e) {
            Console.WriteLine();
            Console.WriteLine(e);
        } finally {
            Console.WriteLine();
            WaitForKey("Press any key to close console...");
        }
    }

    private static void WaitForKey(string message) {
        Console.WriteLine(message);
        Console.ReadKey(true);
    }
}