using Microsoft.Extensions.Hosting;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.FtpPolling.WorkFlow
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

                            var localImagePaths = await _ftpPolling.DownloadFolderAsync(folder);

                            if (localImagePaths.Count == 0)
                            {
                                Logger.LogInfo($"[FTP] Folder '{folder}' contained no images.");
                                continue;
                            }

                            var properties = await _workflow.AnalyzeImagesAsync(localImagePaths);
                            Logger.LogInfo($"[FTP] Analysis complete for folder '{folder}'. Results  .... ");
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
