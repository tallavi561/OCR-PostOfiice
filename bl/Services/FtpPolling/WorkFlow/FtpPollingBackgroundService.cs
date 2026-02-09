using Microsoft.Extensions.Hosting;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.CompanyName;
using CameraAnalyzer.bl.Models;
namespace CameraAnalyzer.bl.Services.FtpPolling.WorkFlow
{
    public class FtpPollingBackgroundService : BackgroundService
    {
        private readonly IFtpPollingService _ftpPolling;
        private readonly IPackagesAnalysisWorkflow _workflow;
        private readonly ILogger<FtpPollingBackgroundService> _logger;
        private readonly ICompanyNameService _companyNameService; // השירות החדש
        // Keeps track of folders that were already handled
        private readonly HashSet<string> _knownFolders = new HashSet<string>();

        public FtpPollingBackgroundService(
            IFtpPollingService ftpPolling,
            IPackagesAnalysisWorkflow workflow,
            ICompanyNameService companyNameService,
            ILogger<FtpPollingBackgroundService> logger)
        {
            _ftpPolling = ftpPolling;
            _workflow = workflow;
            _logger = logger;
            _companyNameService = companyNameService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInfo("FTP Polling Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Step 1: get delivery company name
                    // Step 1: Find all current folders
                    var folders = await _ftpPolling.GetCurrentFoldersAsync();
                    if (folders == null || !folders.Any())
                    {
                        Logger.LogDebug("[FTP] No folders found on FTP server.");
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }
                    //  string deliveryCompanyName = "IsraelPostOffice";
                    string deliveryCompanyName = await _companyNameService.GetCompanyNameAsync();
                    Logger.LogDebug($"[FTP] Using delivery company name: {deliveryCompanyName}");
                    
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

                                var localImagesPaths = await _ftpPolling.DownloadFolderAsync(folder);
                                if (localImagesPaths.Count == 0)
                                {
                                    Logger.LogInfo($"[FTP] Folder '{folder}' contained no images.");
                                    return;
                                }

                                await _ftpPolling.DeleteFolderAndContentsAsync(folder);
                                
                                Logger.LogInfo($"[FTP] Downloaded {localImagesPaths.Count} images from folder '{folder}'. Starting analysis...");
                                List<PackageDetails> properties = await _workflow.AnalyzeImagesAsync(localImagesPaths, deliveryCompanyName);
                                Logger.LogInfo($"[FTP] Analysis returned {properties.Count} packages for folder '{folder}'.");
                                // Log the results
                                foreach (var prop in properties)
                                {
                                    Logger.LogInfo($"[FTP] Analyzed package: {prop}");
                                }

                                Logger.LogInfo($"[FTP] Deleted images for folder '{folder}'.");
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
