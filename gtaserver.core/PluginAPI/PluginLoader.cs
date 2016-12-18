using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GTAServer.PluginAPI
{
    public static class PluginLoader
    {
        public static string Location = System.AppContext.BaseDirectory;
        private static ILogger _logger;
        public static List<IPlugin> LoadPlugin(string targetAssemblyName)
        {
            _logger = Util.LoggerFactory.CreateLogger<GameServer>();
            var assemblyName = targetAssemblyName;
            var pluginList = new List<IPlugin>();


            var pluginAssembly = Assembly.Load(new AssemblyName(assemblyName));
            var types = pluginAssembly.GetExportedTypes();
            var validTypes = types.Where(t => typeof(IPlugin).IsAssignableFrom(t)).ToArray();
            if (!validTypes.Any())
            {
                _logger.LogError("No classes found that extend IPlugin in assembly " + assemblyName);
                return new List<IPlugin>();
            }
            foreach (var plugin in validTypes)
            {
                var curPlugin = Activator.CreateInstance(plugin) as IPlugin;
                if (curPlugin == null) _logger.LogWarning("Could not create instance of " + plugin.Name + " (returned null after Activator.CreateInstance)");
            }
            
            return pluginList;
        }
    }
}
