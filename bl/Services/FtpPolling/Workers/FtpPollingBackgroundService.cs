using Microsoft.Extensions.Hosting;
using CameraAnalyzer.bl.Services.FtpPolling.Interfaces;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.FtpPolling.Workers
{
    public class FtpPollingBackgroundService : BackgroundService
    {
        private readonly IFtpPollingService _ftpPolling;
        private readonly IPackagesAnalysisWorkflow _workflow;
        private readonly ILogger<FtpPollingBackgroundService> _logger;

        private readonly HashSet<string> _knownFolders = new HashSet<string>();

        public FtpPollingBackgroundService(
            IFtpPollingService ftpPolling,
            IPackagesAnalysisWorkflow workflow,
            ILogger<FtpPollingBackgroundService> logger)
        {
            _ftpPolling = ftpPolling;
            _workflow = workflow;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FTP Polling Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var folders = await _ftpPolling.GetCurrentFoldersAsync();

                    foreach (var folder in folders)
                    {
                        if (!_knownFolders.Contains(folder))
                        {
                            _knownFolders.Add(folder);
                            Logger.LogInfo($"[FTP] New folder detected: {folder}");

                            // ⬇️ שלב 1: הורד את כל הקבצים מהתיקייה המרוחקת לנתיב לוקאלי
                            var localImagePaths = await _ftpPolling.DownloadFolderAsync(folder);

                            if (localImagePaths.Count == 0)
                            {
                                Logger.LogInfo($"[FTP] Folder '{folder}' contained no images.");
                                continue;
                            }

                            // ⬇️ שלב 2: העבר את רשימת הקבצים ל-Workflow
                            await _workflow.AnalyzeImagesAsync(localImagePaths);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while polling FTP.");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

    }
}
