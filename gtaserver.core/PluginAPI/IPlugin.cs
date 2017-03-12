namespace GTAServer.PluginAPI
{
    public interface IPlugin
    {
        /// <summary>
        /// Name of the plugin
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Description of the plugin
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Name of the plugin author.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Plugin entry point, called when a plugin is being enabled.
        /// Use this to register any necessary hooks and commands.
        /// </summary>
        /// <param name="gameServer">Game server object.</param>
        /// <param name="isAfterServerLoad">If the plugin is being started after the server has started.</param>
        /// <returns>If the plugin successfully loaded</returns>
        bool OnEnable(GameServer gameServer, bool isAfterServerLoad);
    }
}
