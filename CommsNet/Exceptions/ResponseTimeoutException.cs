using System;
using CommsNet.Structures;

namespace CommsNet.Exceptions {
    public class ResponseTimeoutException : Exception {
        public ResponseTimeoutException(string message, Transmission transmissionObject) : base(message) {
            TransmissionObject = transmissionObject;
        }

        public Transmission TransmissionObject { get; }
    }
}