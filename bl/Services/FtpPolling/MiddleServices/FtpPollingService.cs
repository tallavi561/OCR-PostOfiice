using FluentFTP;
using CameraAnalyzer.bl.Services.FtpPolling.WorkFlow;

namespace CameraAnalyzer.bl.Services.FtpPolling
{
        public interface IFtpPollingService
    {
        Task<List<string>> DownloadFolderAsync(string folderName);

        Task<IEnumerable<string>> GetCurrentFoldersAsync();
    }
    public class FtpPollingService : IFtpPollingService
    {
        private readonly string _host;
        private readonly string _user;
        private readonly string _pass;

        public FtpPollingService(IConfiguration config)
        {
            _host = config["FtpConfig:Host"];
            _user = config["FtpConfig:User"];
            _pass = config["FtpConfig:Password"];
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
        public async Task<List<string>> DownloadFolderAsync(string folderName)
        {
            var localFolder = Path.Combine("appdata", "ftp_downloads", folderName);

            Directory.CreateDirectory(localFolder);

            List<string> downloadedFiles = new List<string>();

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
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                            continue;

                        string localPath = Path.Combine(localFolder, item.Name);

                        var status = await client.DownloadFile(localPath, item.FullName);

                        if (status == FtpStatus.Success)
                        {
                            downloadedFiles.Add(localPath);
                        }
                    }
                }
            }

            return downloadedFiles;
        }
    }
}
