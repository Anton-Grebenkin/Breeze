namespace Shop.Application.Services;

public sealed record SentEmail(string To, string Subject, string Body);
