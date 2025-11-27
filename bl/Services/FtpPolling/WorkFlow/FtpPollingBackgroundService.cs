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

        // Keeps track of folders that were already handled
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
            Logger.LogInfo("FTP Polling Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Step 1: Find all current folders
                    var folders = await _ftpPolling.GetCurrentFoldersAsync();

                    // Step 2: Collect only the new folders
                    List<string> newFolders = new List<string>();
                    foreach (var folder in folders)
                    {
                        if (_knownFolders.Add(folder))
                        {
                            Logger.LogInfo($"[FTP] New folder detected: {folder}");
                            newFolders.Add(folder);
                        }
                    }

                    // Step 3: Run processing for all new folders in parallel
                    List<Task> tasks = new List<Task>();

                    foreach (var folder in newFolders)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                Logger.LogInfo($"[TASK] Start processing folder: {folder}");

                                var localImagePaths = await _ftpPolling.DownloadFolderAsync(folder);
                                if (localImagePaths.Count == 0)
                                {
                                    Logger.LogInfo($"[FTP] Folder '{folder}' contained no images.");
                                    return;
                                }

                                var properties = await _workflow.AnalyzeImagesAsync(localImagePaths);

                                Logger.LogInfo($"[FTP] Analysis complete for folder '{folder}'.");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"[TASK] Error while processing folder '{folder}': {ex.Message}");
                            }

                        }, stoppingToken));
                    }

                    // Step 4: Wait for all tasks to finish (parallel)
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error while polling FTP server: " + ex.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
