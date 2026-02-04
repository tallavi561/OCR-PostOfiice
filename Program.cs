
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Services.FtpPolling;
using CameraAnalyzer.bl.Services.FtpPolling.WorkFlow;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
// --- תוספת: using לשירות החדש ---
using CameraAnalyzer.bl.Services.CompanyName; 

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Configuration Settings (Options Pattern)
// -------------------------
// --- תוספת: קשירת ה-appsettings למחלקת ההגדרות ---
builder.Services.Configure<LabelsCompanyOptions>(
    builder.Configuration.GetSection("LabelsCompany"));


// -------------------------
// External APIs
// -------------------------
// AiDetector: uses custom BaseAddress
builder.Services.AddHttpClient<AiDetectorAPI>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});

// --- תוספת: רישום השירות החדש כ-Typed HttpClient ---
builder.Services.AddHttpClient<ICompanyNameService, CompanyNameService>();

// Services
// (הערה: builder.Services.AddHttpClient<AiDetectorAPI>(); הופיע כאן שוב, מספיק הרישום למעלה)

builder.Services.AddSingleton<DetectionService>();
builder.Services.AddSingleton<GeminiLabelService>();
builder.Services.AddSingleton<WorkflowOutputService>();

// GeminiAPI
builder.Services.AddHttpClient<GeminiAPI>();



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