
using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;
using CameraAnalyzer.bl.Services.FtpPolling;
using CameraAnalyzer.bl.Services.FtpPolling.WorkFlow;
using CameraAnalyzer.bl.Services.PackagesAnalysis.MiddleServices;
// --- תוספת: using לשירות החדש ---
using CameraAnalyzer.bl.Services.CompanyName;
using CameraAnalyzer.bl.Services.StickersExtractor.Workflow;
using CameraAnalyzer.bl.Models.DetectorModles.Labels;
using CameraAnalyzer.bl.Services.StickersExtractor.MiddleServices;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// Configuration Settings (Options Pattern)
// -------------------------
// --- תוספת: קשירת ה-appsettings למחלקת ההגדרות ---
builder.Services.Configure<LabelsCompanyOptions>(
    builder.Configuration.GetSection("LabelsCompany"));




// --- תוספת: רישום השירות החדש כ-Typed HttpClient ---
builder.Services.AddHttpClient<ICompanyNameService, CompanyNameService>();

// Services
// 1. הגדרת רשימת המדבקות לטעינה (אפשר להביא מ-Configuration)
var labelDefinitions = new List<InputLabelDefinition>
{
    new InputLabelDefinition("DHL", "prototypeLabels/DHL.jpg"),
    new InputLabelDefinition("FED-EX", "prototypeLabels/FED-EX.jpg"),
    new InputLabelDefinition("IsraelPostOffice", "prototypeLabels/IsraelPostOffice.jpg")
};

// 2. רישום ה-Detector כ-Singleton כי הוא טוען SIFT Features כבדים ב-Constructor
builder.Services.AddSingleton(new StickersDetector(labelDefinitions));

// 3. רישום ה-Service שמשתמש ב-Detector
builder.Services.AddSingleton<StickersExtractorService>();

// 4. רישום שאר שירותי הניתוח
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