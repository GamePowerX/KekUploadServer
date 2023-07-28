using System.Reflection;
using KekUploadServerApi;

namespace KekUploadServer.Plugins;

public class PluginLoader
{
    public static PluginApi PluginApi = null!;
    private ILogger<PluginLoader>? _logger;
    
    public async Task LoadPlugins(WebApplication app, ILogger<PluginLoader>? logger = null)
    {
        var pluginApi = new PluginApi(app);
        var config = app.Configuration;
        var pluginDirectory = config.GetValue<string>("PluginDirectory") ?? "plugins";
        var pluginDirectoryPath = Path.GetFullPath(pluginDirectory);
        if (!Directory.Exists(pluginDirectoryPath))
        {
            Directory.CreateDirectory(pluginDirectoryPath);
        }
        var pluginFiles = Directory.GetFiles(pluginDirectoryPath, "*.dll");
        var pluginList = new List<IPlugin>();
        foreach (var pluginFile in pluginFiles)
        {
            var pluginAssembly = Assembly.LoadFrom(pluginFile);
            var pluginTypes = pluginAssembly.GetTypes();
            foreach (var pluginType in pluginTypes)
            {
                if (pluginType is not { IsClass: true, IsAbstract: false } ||
                    !pluginType.IsAssignableTo(typeof(IPlugin))) continue;
                var plugin = (IPlugin?) Activator.CreateInstance(pluginType);
                if (plugin == null)
                {
                    logger?.LogError("Failed to create plugin instance for {PluginType}", pluginType);
                    continue;
                }
                pluginList.Add(plugin);
            }
        }
        var pluginLoadOrder = new List<string>();
        foreach (var plugin in pluginList)
        {
            // Check if plugin has dependencies
            if (plugin.Info.Dependencies.Length == 0)
            {
                if(!pluginLoadOrder.Contains(plugin.Info.Name)) 
                    pluginLoadOrder.Add(plugin.Info.Name);
                continue;
            }
            // Check if the dependencies are in the plugin list
            var dependencies = plugin.Info.Dependencies;
            var dependencyFound = true;
            foreach (var dependency in dependencies)
            {
                var dependencyPlugin = pluginList.Find(p => p.Info.Name == dependency);
                if (dependencyPlugin != null) continue;
                logger?.LogError("Plugin {PluginName} has dependency {Dependency} which was not found!",
                    plugin.Info.Name, dependency);
                dependencyFound = false;
                break;
            }
            // Order the plugin behind its dependencies
            if (!dependencyFound) continue;
            {
                // check if the plugin load order already contains the dependencies
                var notFoundDependencies = new List<string>();
                foreach (var dependency in dependencies)
                {
                    if (pluginLoadOrder.Contains(dependency)) continue;
                    notFoundDependencies.Add(dependency);
                    break;
                }
                if (notFoundDependencies.Count == 0)
                {
                    pluginLoadOrder.Add(plugin.Info.Name);
                }
                else
                {
                    // Add the dependencies to the end of the list
                    pluginLoadOrder.AddRange(notFoundDependencies);
                    // Add the plugin to the end of the list after the dependencies
                    pluginLoadOrder.Add(plugin.Info.Name);
                }
            }
        }
        // Order the plugin list by the plugin load order
        var orderedPluginList = new List<IPlugin>();
        foreach (var pluginName in pluginLoadOrder)
        {
            var plugin = pluginList.Find(p => p.Info.Name == pluginName);
            if (plugin == null)
            {
                logger?.LogError("Plugin {PluginName} was not found!", pluginName);
                continue;
            }
            orderedPluginList.Add(plugin);
        }
        // Load plugins
        foreach (var plugin in orderedPluginList)
        {
            logger?.LogInformation("Loading plugin {PluginName}", plugin.Info.Name);
            await pluginApi.AddAndLoadPlugin(plugin);
        }
        PluginApi = pluginApi;
        _logger = logger;
    }

    public async Task StartPlugins()
    {
        foreach (var plugin in PluginApi.Plugins.Keys)
        {
            _logger?.LogInformation("Starting plugin {PluginName}", plugin.Info.Name);
            await plugin.Start();
            PluginApi.Plugins[plugin] = PluginState.Enabled;
        }
    }

    public async Task StopPlugins()
    {
        // stop in reverse order
        for (var i = PluginApi.Plugins.Count - 1; i >= 0; i--)
        {
            var plugin = PluginApi.Plugins.Keys.ElementAt(i);
            _logger?.LogInformation("Stopping plugin {PluginName}", plugin.Info.Name);
            await PluginApi.UnloadPlugin(plugin);
        }
    }
}