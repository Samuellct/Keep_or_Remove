using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.KeepOrRemove.Inject;

/// <summary>
/// Hosted service that runs once at server startup to register the single index.html web
/// transformation via the FileTransformation plugin. Registered explicitly via
/// <c>AddHostedService</c> in <see cref="PluginServiceRegistrator"/> (an <see cref="IHostedService"/>
/// does not appear in Dashboard &gt; Scheduled Tasks, unlike an <c>IScheduledTask</c>).
/// </summary>
public sealed class StartupService : IHostedService
{
    private readonly ILogger<StartupService> _logger;
    private readonly FileTransformationDetector _detector;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="detector">FileTransformation reflection bridge.</param>
    public StartupService(ILogger<StartupService> logger, FileTransformationDetector detector)
    {
        _logger = logger;
        _detector = detector;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_detector.IsAvailable())
        {
            const string warning =
                "FileTransformation plugin is not installed. Keep or Remove cannot inject its vote "
                + "buttons into the web client. Install jellyfin-plugin-file-transformation and restart Jellyfin.";

            _logger.LogError("[KeepOrRemove] {Warning}", warning);
            SetStartupWarning(warning);
            return Task.CompletedTask;
        }

        SetStartupWarning(null);
        RegisterIndexHtmlTransformation();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void RegisterIndexHtmlTransformation()
    {
        var payload = new JObject
        {
            ["id"] = Plugin.Instance?.Id.ToString() ?? Guid.NewGuid().ToString(),
            ["fileNamePattern"] = "index.html",
            ["callbackAssembly"] = typeof(TransformationPatches).Assembly.FullName,
            ["callbackClass"] = typeof(TransformationPatches).FullName,
            ["callbackMethod"] = nameof(TransformationPatches.IndexHtml)
        };

        _detector.RegisterTransformation(payload);
        _logger.LogInformation("[KeepOrRemove] Registered index.html transformation.");
    }

    private void SetStartupWarning(string? warning)
    {
        if (Plugin.Instance is null || Plugin.Instance.Configuration.StartupWarning == warning)
        {
            return;
        }

        Plugin.Instance.Configuration.StartupWarning = warning;
        Plugin.Instance.SaveConfiguration();
    }
}
