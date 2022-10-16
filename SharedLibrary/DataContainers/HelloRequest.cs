using MessagePack;

namespace SharedLibrary.DataContainers
{
    [MessagePackObject]
    public class HelloRequest
    {
        [Key(0)] public string Greetings { get; set; }

        [Key(1)] public float SomeFloat { get; set; }
    }
}