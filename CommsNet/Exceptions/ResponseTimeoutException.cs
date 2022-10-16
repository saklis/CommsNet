using CommsNet.Structures;
using System;

namespace CommsNet.Exceptions
{
    public class ResponseTimeoutException : Exception
    {
        public ResponseTimeoutException(string message, Transmission transmissionObject) : base(message) => TransmissionObject = transmissionObject;

        public Transmission TransmissionObject { get; }
    }
}