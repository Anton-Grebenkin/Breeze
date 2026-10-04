using CodeEditor.Modules.Docker.Services;

namespace CodeEditor.Modules.Docker.Tests;

/// <summary>Secrets in <c>docker inspect</c> output: values hidden, names visible, the rest unchanged.</summary>
public sealed class InspectSecretsTests
{
    [Fact]
    public void SecretValues_AreHidden_EverywhereInTheTree()
    {
        const string json = """
            [{"Config": {"Env": ["DB_PASSWORD=p@ss", "API_TOKEN=t0ken", "PATH=/usr/bin", "EMPTY_SECRET="], "Cmd": ["sh", "-c", "a && b"]},
              "ContainerConfig": {"Env": ["AWS_SECRET_ACCESS_KEY=xyz"]}}]
            """;

        var hidden = InspectSecrets.Hide(json);

        Assert.DoesNotContain("p@ss", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("t0ken", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", hidden, StringComparison.Ordinal);
        Assert.Contains("\"DB_PASSWORD=***\"", hidden, StringComparison.Ordinal);
        Assert.Contains("\"PATH=/usr/bin\"", hidden, StringComparison.Ordinal);
        Assert.Contains("\"EMPTY_SECRET=\"", hidden, StringComparison.Ordinal);
        Assert.Contains("a && b", hidden, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutSecrets_OrNotJson_TextStaysAsIs()
    {
        const string plain = """[{"Config": {"Env": ["PATH=/usr/bin"]}}]""";

        Assert.Same(plain, InspectSecrets.Hide(plain));
        Assert.Equal("Error: No such object: api", InspectSecrets.Hide("Error: No such object: api"));
    }
}
