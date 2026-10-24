using System;
using Dalamud.Plugin.Services;

namespace BetterDresser;

internal sealed class Service : IDisposable
{
    public static Configuration PluginConfig { get; private set; } = null!;
    public static ItemCache ItemCache { get; private set; } = null!;

    public Service()
    {
        PluginConfig = (Configuration)Plugin.PluginInterface.GetPluginConfig()! ?? new Configuration();
        ItemCache = new();
    }

    public void Dispose()
    {
        ItemCache.Dispose();
    }
}

// @Kami
public static class ServiceExtension {
    /// <summary>
    /// Static class to hold the instance reference.
    /// </summary>
    private static class ServiceInstance<T> where T : class, IDalamudService {
        public static T? Instance => field ??= Plugin.PluginInterface.GetService(typeof(T)) as T;
    }

    /// <summary>
    /// Extension provider to allow you to .Get() from the interface type.
    /// </summary>
    extension<T>(T) where T : class, IDalamudService {
        public static T Get() => ServiceInstance<T>.Instance ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found.");
    }
}
