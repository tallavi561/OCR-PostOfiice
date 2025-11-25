using FluentFTP;
using CameraAnalyzer.bl.Services.FtpPolling.Interfaces;

namespace CameraAnalyzer.bl.Services.FtpPolling
{
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
    }
}
