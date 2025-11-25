namespace CameraAnalyzer.bl.Services.FtpPolling.Interfaces
{
    public interface IFtpPollingService
    {
        Task<IEnumerable<string>> GetCurrentFoldersAsync();
    }
}
