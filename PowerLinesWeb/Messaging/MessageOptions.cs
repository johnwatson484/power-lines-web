namespace PowerLinesWeb.Messaging;

public class MessageOptions
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FixtureQueue { get; set; }
    public string FixtureSubscription { get; set; }
    public string ResultQueue { get; set; }
    public string ResultSubscription { get; set; }
}
