using MessagePack;

namespace SharedLibrary.DataContainers {
    [MessagePackObject]
    public class HelloResponse {
        [Key(0)] public string Reply { get; set; }
    }
}