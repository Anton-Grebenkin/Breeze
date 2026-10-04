namespace Shop.Application.Services;

/// <summary>Почта в памяти: письма складываются в список — его проверяют тесты.</summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    public List<SentEmail> Sent { get; } = [];

    public void Send(string to, string subject, string body) => Sent.Add(new SentEmail(to, subject, body));
}
