using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Probes;
using CodeEditor.Modules.Agent.Services.Settings;

namespace CodeEditor.UI.Tests.Agent;

/// <summary>
/// "Check the model" on a real model. It spends tokens, so it runs only when <c>BREEZE_REAL_MODEL</c> names the
/// model (for example <c>deepseek/deepseek-v4.1-flash</c>). The service is the editor's default one, reached as a
/// plain connection of the user's own, with the user's key read from their editor data. The report is kept next to
/// the screenshots; the key is in neither the report nor the test output.
/// </summary>
public sealed class ModelProbeRealModelTests
{
    [Fact]
    public async Task RealModel_IsChecked_AndTheReportIsKept()
    {
        var model = Environment.GetEnvironmentVariable(AgentBrowserRealModelTests.ModelVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(model), $"Прогон на настоящей модели тратит токены: задайте {AgentBrowserRealModelTests.ModelVariable}.");
        var key = new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths(root: null)).Get(AgentSecrets.AitunnelApiKey);
        Assert.True(key is not null, "В данных редактора нет ключа сервиса моделей.");

        // The service wants the id without the vendor.
        var id = model![(model.LastIndexOf('/') + 1)..];
        var connection = new AgentConnection { Id = "real", Endpoint = AgentServices.AitunnelEndpoint };
        var started = DateTime.UtcNow;

        var report = await new ModelProbe().RunAsync(connection, key, id, TestContext.Current.CancellationToken);

        var text = new StringBuilder($"Модель: {id}, сервис: {connection.Endpoint}, время: {(DateTime.UtcNow - started).TotalSeconds:0.0} с\n");
        foreach (var step in report.Steps)
        {
            text.Append(step.Passed switch { true => "+ ", false => "- ", _ => "  " }).Append(step.Kind).Append(": ").Append(step.Detail).Append('\n');
        }

        text.Append($"Предложено: изображения — {report.Images}, рассуждения — {report.Reasoning}, старое поле предела — {report.LegacyMaxTokens}\n");
        var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "real-model")).FullName;
        File.WriteAllText(Path.Combine(output, "model-probe.txt"), text.ToString());

        Assert.True(report.Steps[0].Passed, text.ToString());
    }
}
