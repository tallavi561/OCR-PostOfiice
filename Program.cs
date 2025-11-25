using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Services.FtpPolling;
using CameraAnalyzer.bl.Services.FtpPolling.Interfaces;
using CameraAnalyzer.bl.Services.FtpPolling.Workers;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Controllers + Swagger
// -------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -------------------------
// External APIs
// -------------------------
builder.Services.AddSingleton<GeminiAPI>();
builder.Services.AddHttpClient<GoogleVisionAPI>();

// -------------------------
// Packages Analysis Workflow
// -------------------------
builder.Services.AddSingleton<IPackagesAnalysisWorkflow, PackagesAnalysisWorkflow>();

// -------------------------
// FTP Polling Services
// -------------------------
builder.Services.AddSingleton<IFtpPollingService, FtpPollingService>();
builder.Services.AddHostedService<FtpPollingBackgroundService>();

// -------------------------
// App settings (optional)
// -------------------------
// Strongly Typed Config:
// builder.Services.Configure<FtpConfig>(builder.Configuration.GetSection("FtpConfig"));

var app = builder.Build();

// -------------------------
// Swagger
// -------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// -------------------------
// Pipeline
// -------------------------
app.UseAuthorization();
app.MapControllers();

// -------------------------
// Run
// -------------------------
app.Run();
