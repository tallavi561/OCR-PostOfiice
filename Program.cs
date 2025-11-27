using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Services.FtpPolling;
using CameraAnalyzer.bl.Services.FtpPolling.WorkFlow;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
var builder = WebApplication.CreateBuilder(args);

// -------------------------
// External APIs
// -------------------------
// AiDetector: uses custom BaseAddress
builder.Services.AddHttpClient<AiDetectorAPI>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});


// Services
builder.Services.AddHttpClient<AiDetectorAPI>();

builder.Services.AddSingleton<DetectionService>();
builder.Services.AddSingleton<CroppingService>();
builder.Services.AddSingleton<GeminiLabelService>();
builder.Services.AddSingleton<WorkflowOutputService>();

builder.Services.AddSingleton<IPackagesAnalysisWorkflow, PackagesAnalysisWorkflow>();




// GeminiAPI - if it needs HttpClient
builder.Services.AddHttpClient<GeminiAPI>();

// Google Vision
builder.Services.AddHttpClient<GoogleVisionAPI>();

// -------------------------
// Packages Analysis Workflow
// -------------------------
builder.Services.AddSingleton<IPackagesAnalysisWorkflow, PackagesAnalysisWorkflow>();

// -------------------------
// FTP Polling
// -------------------------
builder.Services.AddSingleton<IFtpPollingService, FtpPollingService>();
builder.Services.AddHostedService<FtpPollingBackgroundService>();

// -------------------------
// Controllers + Swagger
// -------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
