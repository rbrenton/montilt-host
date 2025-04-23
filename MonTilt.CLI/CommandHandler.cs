using MonTilt.Core;
using MonTilt.Driver;

namespace MonTilt.CLI
{
    public class CommandHandler
    {
        private readonly ConfigManager _configManager;
        private readonly MonitorOrientationDriver _driver;
        private BackgroundService _backgroundService;

        public CommandHandler(ConfigManager configManager, MonitorOrientationDriver driver)
        {
            _configManager = configManager;
            _driver = driver;
        }

        public void StartService(Action<string> logCallback)
        {
            _backgroundService = new BackgroundService(_driver, logCallback);
            _backgroundService.Start();
        }

        public void StopService()
        {
            _backgroundService?.Stop();
        }
    }
}