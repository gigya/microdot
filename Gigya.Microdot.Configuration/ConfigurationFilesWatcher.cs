using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Gigya.Microdot.Configuration
{
    /// <sumarry>
    /// This class monitors changes in config files and emits an event through <see cref="DataChanges"/> when one or
    /// more monitored configuration files were modified. Since config files are typically updated through a git pull
    /// which can take several seconds to complete, we wait till all updates are done and fire the event once no updates
    /// were made for 5 seconds.
    /// </sumarry>
    /// <remarks>
    /// Note that the FileSystemWatcher class holds a handle to a folder, which could cause it to become locked,
    /// preventing configuration changes. Also, if a folder is renamed/deleted an another one is created with the same
    /// name later, the new one won't be monitored. Hence, we monitor changes from the configuration root, recursively,
    /// instead of using one FileSystemWatcher per folder. We assume the configuration root is never deleted. 
    /// </remarks>
    public sealed class ConfigurationFilesWatcher : IConfigurationDataWatcher, IDisposable
    {
        private const int INTERNAL_BUFFER_SIZE = 64 * 1024;//The maximum allowed value according to MS

        public ISourceBlock<bool> DataChanges { get; }

        private FileSystemWatcher _rootWatcher;

        private BroadcastBlock<bool> Broadcaster { get; } = new BroadcastBlock<bool>(null);

        private IConfigurationLocationsParser ConfigurationLocationsParser { get; }

        private readonly Timer refreshTimer;

        public ConfigurationFilesWatcher(IConfigurationLocationsParser configurationLocationsParser)
        {
            ConfigurationLocationsParser = configurationLocationsParser;
            DataChanges = Broadcaster;
            refreshTimer = new Timer(_ => Broadcaster.Post(true));
            CreateRootWatcher();
        }


        private void CreateRootWatcher()
        {
            
            _rootWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(ConfigurationLocationsParser.ConfigRoot),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                InternalBufferSize = INTERNAL_BUFFER_SIZE,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.config"
            };

            _rootWatcher.Created += OnRootChanged;
            _rootWatcher.Renamed += OnRootChanged;
            _rootWatcher.Changed += OnRootChanged;
            _rootWatcher.Deleted += OnRootChanged;
        }


        private void OnRootChanged(object sender, FileSystemEventArgs e)
        {
            if(!e.FullPath.Contains(".git"))
            {
                // Schedule a config reload in 5 seconds from now. If more config files are changed till then, we'll keep
                // pushing back the reload time 5 seconds away, till no more file updates are encountered for 5 seconds.
                refreshTimer.Change(5000, Timeout.Infinite);
            }
        }


        public void Dispose()
        {
            _rootWatcher.Dispose();
            refreshTimer.Dispose();
        }
    }
}
