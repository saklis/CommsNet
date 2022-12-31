using System;
using MessagePack;

namespace CommsNet.Structures {
    /// <summary>
    ///     Supported transmission types.
    /// </summary>
    public enum TransmissionType {
        Request  = 0,
        Response = 1
    }

    [Serializable]
    [MessagePackObject]
    public class Transmission {
        /// <summary>
        ///     Transmission's expiration time. At this time transmission will be removed from received collection.
        /// </summary>
        [Key(1)]
        public DateTime ExpirationTime { get; set; } = DateTime.MaxValue;

        /// <summary>
        ///     Transmitted type.
        /// </summary>
        [Key(0)]
        public bool HasContent { get; set; }

        /// <summary>
        ///     Returned type.
        /// </summary>
        [Key(4)]
        public bool HasReturn { get; set; }

        /// <summary>
        ///     Transmission identity. Used for building request-response pairs.
        /// </summary>
        [Key(2)]
        public Guid Identity { get; set; }

        /// <summary>
        ///     Name of the method that needs to be remotely executed.
        /// </summary>
        [Key(3)]
        public string MethodName { get; set; }

        /// <summary>
        ///     ServiceManager identity. Defines a target client on which method will be executed. May
        ///     be 'default' or Guid.Empty if method is executed on the server.
        /// </summary>
        [Key(5)]
        public Guid SessionIdentity { get; set; }

        /// <summary>
        ///     This transmission's type.
        /// </summary>
        [Key(6)]
        public int Type { get; set; }
    }
}