using FluentFTP;
using CameraAnalyzer.bl.Services.FtpPolling.WorkFlow;
using CameraAnalyzer.bl.Utils;

namespace CameraAnalyzer.bl.Services.FtpPolling
{
    public interface IFtpPollingService
    {
        Task<List<ImageFromFtp>> DownloadFolderAsync(string folderName);

        Task<IEnumerable<string>> GetCurrentFoldersAsync();
        Task DeleteFilesAsync(List<string> filePaths);
        Task DeleteFolderImagesAsync(string folderName);
        Task DeleteFolderAndContentsAsync(string folderName);

    }

    public class ImageFromFtp
    {
        public string ImageName { get; set; }
        public byte[] ImageBytes { get; set; }
    }
    public class FtpPollingService : IFtpPollingService
    {
        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;

        public FtpPollingService(IConfiguration config)
        {
            _host = config["FtpConfig:Host"]!;
            _user = config["FtpConfig:User"]!;
            _pass = config["FtpConfig:Password"]!;
        }

        public async Task<IEnumerable<string>> GetCurrentFoldersAsync()
        {
            using (var client = new AsyncFtpClient(_host, _user, _pass))
            {
                await client.Connect();

                var listing = await client.GetListing("/");

                return listing
                    .Where(x => x.Type == FtpObjectType.Directory)
                    .Select(x => x.Name)
                    .ToList();
            }
        }
        public async Task<List<ImageFromFtp>> DownloadFolderAsync(string folderName)
        {
            List<ImageFromFtp> images = new List<ImageFromFtp>();

            using (var client = new AsyncFtpClient(_host, _user, _pass))
            {
                await client.Connect();

                string remoteFolderPath = "/" + folderName;

                var items = await client.GetListing(remoteFolderPath);

                foreach (var item in items)
                {
                    if (item.Type == FtpObjectType.File)
                    {
                        string ext = Path.GetExtension(item.Name).ToLower();

                        try
                        {
                            // Download file directly to memory
                            using (var memoryStream = new MemoryStream())
                            {
                                var status = await client.DownloadStream(memoryStream, item.FullName);

                                if (status)
                                {
                                    images.Add(new ImageFromFtp
                                    {
                                        ImageName = item.Name,
                                        ImageBytes = memoryStream.ToArray()
                                    });

                                   Logger.LogInfo($"[INFO] Downloaded to memory: {item.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed downloading {item.Name} | {ex.Message}");
                        }
                    }
                }
            }

            return images;
        }
        public async Task DeleteFilesAsync(List<string> filePaths)
        {
            using (var client = new AsyncFtpClient(_host, _user, _pass))
            {
                await client.Connect();

                foreach (var path in filePaths)
                {
                    try
                    {
                        await client.DeleteFile(path);

                        // verify
                        bool exists = await client.FileExists(path);

                        if (exists)
                            Console.WriteLine($"[WARN] File still exists after delete: {path}");
                        else
                            Console.WriteLine($"[INFO] Successfully deleted: {path}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed deleting: {path} | {ex.Message}");
                    }
                }
            }
        }

        public async Task DeleteFolderImagesAsync(string folderName)
        {
            using (var client = new AsyncFtpClient(_host, _user, _pass))
            {
                await client.Connect();

                string remoteFolderPath = "/" + folderName;

                var items = await client.GetListing(remoteFolderPath);

                foreach (var item in items)
                {
                    if (item.Type == FtpObjectType.File)
                    {
                        try
                        {
                            await client.DeleteFile(item.FullName);
                            Console.WriteLine($"[INFO] Deleted {item.FullName}");
                        }
                        catch (Exception ex)
                        {
                            // Error handling for each file, but continue deleting the rest
                            Console.WriteLine($"[ERROR] Failed deleting {item.FullName} | {ex.Message}");
                        }
                    }
                }
            }
        }

        public async Task DeleteFolderAndContentsAsync(string folderName)
        {
            // Create a new FTP client instance with connection credentials
            using (var client = new AsyncFtpClient(_host, _user, _pass))
            {
                // Establish connection to the FTP server
                await client.Connect();

                // Ensure the remote path starts with a leading slash
                string remoteFolderPath = folderName.StartsWith("/")
                    ? folderName
                    : "/" + folderName;

                try
                {
                    // This command deletes the directory and ALL its contents
                    // (files and subdirectories) recursively
                    // In FluentFTP, DeleteDirectory performs recursive deletion by default
                    await client.DeleteDirectory(remoteFolderPath);

                   Logger.LogInfo(
                        $"[INFO] Successfully deleted folder and all contents: {remoteFolderPath}"
                    );
                }
                catch (Exception ex)
                {
                    // Log the error if deletion fails
                    Logger.LogError(
                        $"[ERROR] Failed to delete folder {remoteFolderPath} | {ex.Message}"
                    );

                    // Re-throw the exception so the caller knows the operation failed
                    throw;
                }
            }
        }


    }
}
