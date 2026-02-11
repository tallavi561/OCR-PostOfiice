using Microsoft.Extensions.Hosting;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Utils;
using CameraAnalyzer.bl.Services.CompanyName;
using CameraAnalyzer.bl.Models;
using CameraAnalyzer.bl.Services.StickersExtractor.Workflow;

/**

ExecuteAsync - Main loop that continuously polls the FTP server for new folders. For each new folder, it triggers the processing workflow.
  └─> PollAndProcessFoldersAsync
        ├─> GetNewFolders
        └─> ProcessSingleFolderAsync (paralleled) - Handles the processing of a single folder, including downloading images, deleting the folder from FTP, and analyzing each image.
              └─> ProcessAllImagesAsync
                    └─> ProcessSingleImageAsync (paralleled) - Handles the processing of a single image, including sticker extraction and analysis.
                          └─> AnalyzeAllStickersAsync
                                └─> AnalyzeSingleStickerAsync (paralleled) - Analyzes a single sticker using the PackagesAnalysisWorkflow and logs the results.
**/

namespace CameraAnalyzer.bl.Services.FtpPolling.WorkFlow
{
    public class FtpPollingBackgroundService : BackgroundService
    {
        private readonly IFtpPollingService _ftpPolling;
        private readonly IPackagesAnalysisWorkflow _analyser;
        private readonly ILogger<FtpPollingBackgroundService> _logger;
        private readonly ICompanyNameService _companyNameService;
        private readonly HashSet<string> _knownFolders = new HashSet<string>();
        private readonly StickersExtractorService _extractorService;

        public FtpPollingBackgroundService(
            StickersExtractorService extractorService,
            IFtpPollingService ftpPolling,
            IPackagesAnalysisWorkflow workflow,
            ICompanyNameService companyNameService,
            ILogger<FtpPollingBackgroundService> logger)
        {
            _extractorService = extractorService;
            _ftpPolling = ftpPolling;
            _analyser = workflow;
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
                    await PollAndProcessFoldersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error while polling FTP server: " + ex.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task PollAndProcessFoldersAsync(CancellationToken stoppingToken)
        {
            // Step 1: Get folders from FTP
            var folders = await _ftpPolling.GetCurrentFoldersFromFtpAsync();
            if (folders == null || !folders.Any())
            {
                Logger.LogDebug("[FTP] No folders found on FTP server.");
                return;
            }

            // Step 2: Get company name
            string deliveryCompanyName = await _companyNameService.GetCompanyNameAsync();
            Logger.LogDebug($"[FTP] Using delivery company name: {deliveryCompanyName}");

            // Step 3: Identify new folders
            var newFolders = GetNewFolders(folders);
            if (!newFolders.Any())
            {
                return;
            }

            // Step 4: Process all new folders in parallel
            var folderTasks = newFolders.Select(folder => 
                ProcessSingleFolderAsync(folder, deliveryCompanyName, stoppingToken));
            
            await Task.WhenAll(folderTasks);
        }

        private List<string> GetNewFolders(IEnumerable<string> folders)
        {
            var newFolders = new List<string>();
            foreach (var folder in folders)
            {
                if (_knownFolders.Add(folder))
                {
                    Logger.LogInfo($"[FTP] New folder detected: {folder}");
                    newFolders.Add(folder);
                }
            }
            return newFolders;
        }

        private async Task ProcessSingleFolderAsync(
            string folder, 
            string deliveryCompanyName, 
            CancellationToken stoppingToken)
        {
            try
            {
                Logger.LogInfo($"[FOLDER] Start processing folder: {folder}");

                // Download images
                var imagesFromFTP = await _ftpPolling.DownloadFolderFromFtpAsync(folder);
                if (imagesFromFTP.Count == 0)
                {
                    Logger.LogInfo($"[FOLDER] Folder '{folder}' contained no images.");
                    return;
                }

                // Delete folder from FTP
                await _ftpPolling.DeleteFolderAndContentsAsync(folder);
                Logger.LogInfo($"[FOLDER] Deleted folder '{folder}' from FTP.");

                // Process all images in parallel
                var allPackages = await ProcessAllImagesAsync(imagesFromFTP, folder, deliveryCompanyName);

                Logger.LogInfo($"[FOLDER] Completed processing folder '{folder}'. Total packages: {allPackages.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[FOLDER] Error while processing folder '{folder}': {ex.Message}");
            }
        }

        private async Task<List<PackageDetails>> ProcessAllImagesAsync(
            List<ImageFromFtp> images, 
            string folderName, 
            string deliveryCompanyName)
        {
            var imageProcessingTasks = images.Select(image => 
                ProcessSingleImageAsync(image, folderName, deliveryCompanyName));

            var results = await Task.WhenAll(imageProcessingTasks);

            // Flatten all results into a single list
            return results.SelectMany(r => r).ToList();
        }

        private async Task<List<PackageDetails>> ProcessSingleImageAsync(
            ImageFromFtp image, 
            string folderName, 
            string deliveryCompanyName)
        {
            try
            {
                Logger.LogInfo($"[IMAGE] Processing image from folder '{folderName}'...");

                // Extract stickers (CPU-bound, synchronous operation)
                var extractedStickers = _extractorService.DetectStickers(
                    image.ImageBytes, 
                    deliveryCompanyName);

                if (extractedStickers == null || extractedStickers.Count == 0)
                {
                    Logger.LogInfo($"[IMAGE] No stickers found in image from folder '{folderName}'.");
                    return new List<PackageDetails>();
                }

                Logger.LogInfo($"[IMAGE] Extracted {extractedStickers.Count} stickers from folder '{folderName}'.");

                // Analyze all stickers in parallel
                var allPackages = await AnalyzeAllStickersAsync(extractedStickers, folderName, deliveryCompanyName);

                return allPackages;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[IMAGE] Error processing image from folder '{folderName}': {ex.Message}");
                return new List<PackageDetails>();
            }
        }

        private async Task<List<PackageDetails>> AnalyzeAllStickersAsync(
            List<DetectionResponse> stickers, 
            string folderName, 
            string deliveryCompanyName)
        {
            var analysisTasks = stickers.Select(sticker => 
                AnalyzeSingleStickerAsync(sticker, folderName, deliveryCompanyName));

            var results = await Task.WhenAll(analysisTasks);

            // Flatten all results into a single list
            return results.SelectMany(r => r).ToList();
        }

        private async Task<List<PackageDetails>> AnalyzeSingleStickerAsync(
            DetectionResponse sticker, 
            string folderName, 
            string deliveryCompanyName)
        {
            try
            {
                Logger.LogInfo($"[STICKER] Analyzing sticker '{sticker.LabelName}' from folder '{folderName}'...");

                var packageDetails = await _analyser.AnalyzeSingleImageAsync(
                    sticker, 
                    deliveryCompanyName);

                Logger.LogInfo($"[STICKER] Analysis returned {packageDetails.Count} packages for '{sticker.LabelName}'.");
                foreach (var package in packageDetails)
                {
                    Logger.LogInfo($"[STICKER] Package details: {package}");
                }
                return packageDetails;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[STICKER] Error analyzing sticker '{sticker.LabelName}' from folder '{folderName}': {ex.Message}");
                return new List<PackageDetails>();
            }
        }
    }
}