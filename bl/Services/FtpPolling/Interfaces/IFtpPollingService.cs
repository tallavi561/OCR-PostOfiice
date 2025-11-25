namespace CameraAnalyzer.bl.Services.FtpPolling.Interfaces
{
    public interface IFtpPollingService
    {
        Task<List<string>> DownloadFolderAsync(string folderName);

        Task<IEnumerable<string>> GetCurrentFoldersAsync();
    }
}
