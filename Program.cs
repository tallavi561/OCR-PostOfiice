using CameraAnalyzer.bl.APIs;
using CameraAnalyzer.bl.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Gemini API
builder.Services.AddSingleton<GeminiAPI>();
// builder.Services.AddScoped<IGeminiService, GeminiService>();

// Google Vision API – חובה!
builder.Services.AddHttpClient<GoogleVisionAPI>();

// Packages Analyzer Service
builder.Services.AddScoped<IPackagesAnalysisWorkflow, PackagesAnalysisWorkflow>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
