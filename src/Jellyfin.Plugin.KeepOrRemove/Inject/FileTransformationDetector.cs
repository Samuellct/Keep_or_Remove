using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.KeepOrRemove.Inject;

/// <summary>
/// Detects and bridges to the FileTransformation plugin at runtime via reflection.
/// Caches the resolved MethodInfo after the first successful lookup. Ported from JellyUX-Homepage.
/// </summary>
public class FileTransformationDetector
{
    private const string AssemblyNameFragment = ".FileTransformation";
    private const string PluginInterfaceTypeName = "Jellyfin.Plugin.FileTransformation.PluginInterface";

    private readonly ILogger<FileTransformationDetector> _logger;
    private MethodInfo? _registerMethod;
    private bool _resolved;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationDetector"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public FileTransformationDetector(ILogger<FileTransformationDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the FileTransformation plugin assembly is loaded and the
    /// RegisterTransformation entry point is reachable. Result is cached after the first success.
    /// </summary>
    /// <returns>True when FileTransformation is available.</returns>
    public bool IsAvailable()
    {
        if (_resolved && _registerMethod is not null)
        {
            return true;
        }

        var assembly = AssemblyLoadContext.All
            .SelectMany(ctx => ctx.Assemblies)
            .FirstOrDefault(a =>
                a.FullName?.Contains(AssemblyNameFragment, StringComparison.Ordinal) ?? false);

        if (assembly is null)
        {
            return false;
        }

        var type = assembly.GetType(PluginInterfaceTypeName);
        if (type is null)
        {
            _logger.LogError(
                "[KeepOrRemove] FileTransformation assembly found but type {Type} is missing.",
                PluginInterfaceTypeName);
            return false;
        }

        _registerMethod = type.GetMethod("RegisterTransformation");
        if (_registerMethod is null)
        {
            _logger.LogError(
                "[KeepOrRemove] FileTransformation type found but RegisterTransformation method is missing.");
            return false;
        }

        _resolved = true;
        return true;
    }

    /// <summary>
    /// Registers a file transformation via the FileTransformation plugin. Must be called only after
    /// <see cref="IsAvailable"/> returns true.
    /// </summary>
    /// <param name="payload">
    /// JObject matching TransformationRegistrationPayload (id, fileNamePattern, callbackAssembly,
    /// callbackClass, callbackMethod).
    /// </param>
    public void RegisterTransformation(JObject payload)
    {
        if (_registerMethod is null)
        {
            _logger.LogError(
                "[KeepOrRemove] RegisterTransformation called but FileTransformation is not available.");
            return;
        }

        try
        {
            _registerMethod.Invoke(null, new object?[] { payload });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KeepOrRemove] FileTransformation RegisterTransformation invocation failed.");
        }
    }
}
