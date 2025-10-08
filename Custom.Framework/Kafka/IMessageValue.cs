namespace Custom.Framework.Kafka
{
    public interface IMessageValue
    {
        string Key { get; }
    }

    public class MessageValue : IMessageValue
    {
        public string Key => Guid.NewGuid().ToString();
    }
}
