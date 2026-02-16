# FTP Polling Background Service 📦

A high-performance, parallel processing background service for monitoring FTP folders, extracting package labels from images, and analyzing them using AI.

## 🎯 Overview

This service continuously monitors an FTP server for new folders containing package images. When new folders are detected, it:

1. **Downloads** images from the FTP server
2. **Extracts** shipping labels/stickers from the images
3. **Analyzes** the labels using AI (Gemini) to extract package details
4. **Processes** everything in parallel for maximum performance

## 🏗️ Architecture

### Service Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    ExecuteAsync (Main Loop)                  │
│                  Runs every 5 seconds                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              PollAndProcessFoldersAsync                      │
│  • Fetch folders from FTP                                    │
│  • Get company name                                          │
│  • Identify new folders                                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│         ProcessSingleFolderAsync (Parallel ⚡)               │
│  • Download all images from folder                           │
│  • Delete folder from FTP                                    │
│  • Process images in parallel                                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│          ProcessSingleImageAsync (Parallel ⚡)               │
│  • Extract stickers from image                               │
│  • Analyze each sticker in parallel                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│        AnalyzeSingleStickerAsync (Parallel ⚡)               │
│  • Send to AI analysis (Gemini)                              │
│  • Extract package details                                   │
└─────────────────────────────────────────────────────────────┘
```

## ⚡ Performance Optimization

### Multi-Level Parallelization

The service implements **three levels of parallelization** for maximum throughput:

#### Level 1: Folder-Level Parallelism
```csharp
// All new folders are processed simultaneously
var folderTasks = newFolders.Select(folder => 
    ProcessSingleFolderAsync(folder, deliveryCompanyName, stoppingToken));
await Task.WhenAll(folderTasks);
```

#### Level 2: Image-Level Parallelism
```csharp
// All images within a folder are processed simultaneously
var imageProcessingTasks = images.Select(image => 
    ProcessSingleImageAsync(image, folderName, deliveryCompanyName));
var results = await Task.WhenAll(imageProcessingTasks);
```

#### Level 3: Sticker-Level Parallelism
```csharp
// All stickers extracted from an image are analyzed simultaneously
var analysisTasks = stickers.Select(sticker => 
    AnalyzeSingleStickerAsync(sticker, folderName, deliveryCompanyName));
var results = await Task.WhenAll(analysisTasks);
```

### Performance Comparison

| Scenario | Serial Processing | Parallel Processing | Speedup |
|----------|------------------|---------------------|---------|
| 3 folders, 10 images each, 2 stickers per image | ~60 seconds | ~1 second | **60x** |
| 1 folder, 50 images, 3 stickers per image | ~150 seconds | ~3 seconds | **50x** |

*Assuming 1 second per operation and sufficient system resources*

## 🔧 Components

### Dependencies

```csharp
private readonly IFtpPollingService _ftpPolling;
private readonly IPackagesAnalysisWorkflow _analyser;
private readonly ICompanyNameService _companyNameService;
private readonly StickersExtractorService _extractorService;
private readonly ILogger<FtpPollingBackgroundService> _logger;
```

### State Management

```csharp
// Tracks processed folders to avoid re-processing
private readonly HashSet<string> _knownFolders = new HashSet<string>();
```

## 📋 Method Documentation

### Core Methods

#### `ExecuteAsync(CancellationToken stoppingToken)`
**Purpose:** Main background service loop  
**Behavior:** Runs continuously, polling every 5 seconds  
**Thread-Safe:** Yes

#### `PollAndProcessFoldersAsync(CancellationToken stoppingToken)`
**Purpose:** Orchestrates the polling and processing workflow  
**Steps:**
1. Fetch folders from FTP
2. Get delivery company name
3. Identify new folders
4. Process all new folders in parallel

#### `ProcessSingleFolderAsync(string folder, string deliveryCompanyName, CancellationToken stoppingToken)`
**Purpose:** Process a single folder end-to-end  
**Steps:**
1. Download all images
2. Delete folder from FTP (cleanup)
3. Process all images in parallel
4. Return aggregated results

**Error Handling:** Catches and logs errors without stopping other folders

#### `ProcessSingleImageAsync(ImageFromFtp image, string folderName, string deliveryCompanyName)`
**Purpose:** Extract and analyze stickers from a single image  
**Returns:** `List<PackageDetails>` - All packages found in the image  
**Error Handling:** Returns empty list on error

#### `AnalyzeSingleStickerAsync(DetectionResponse sticker, string folderName, string deliveryCompanyName)`
**Purpose:** Analyze a single sticker using AI  
**Returns:** `List<PackageDetails>` - Package information extracted from the sticker  
**Error Handling:** Returns empty list on error

## 🛡️ Error Handling Strategy

The service implements **isolated error handling** at each level:

```
❌ Folder Error → Only that folder fails, others continue
❌ Image Error → Only that image fails, others in folder continue
❌ Sticker Error → Only that sticker fails, others in image continue
```

This ensures **maximum resilience** - a single failure never cascades to stop the entire service.

### Example

```csharp
try
{
    Logger.LogInfo($"[STICKER] Analyzing sticker '{sticker.LabelName}'...");
    var packageDetails = await _analyser.AnalyzeSingleImageAsync(sticker, deliveryCompanyName);
    return packageDetails;
}
catch (Exception ex)
{
    Logger.LogError($"[STICKER] Error analyzing sticker: {ex.Message}");
    return new List<PackageDetails>(); // Return empty, don't crash
}
```

## 📊 Logging

The service provides **detailed logging** at each processing stage:

| Level | Tag | Purpose |
|-------|-----|---------|
| `INFO` | `[FTP]` | FTP operations (folders found, deletion) |
| `INFO` | `[FOLDER]` | Folder-level processing |
| `INFO` | `[IMAGE]` | Image-level processing |
| `INFO` | `[STICKER]` | Sticker-level analysis |
| `ERROR` | `[FOLDER]`, `[IMAGE]`, `[STICKER]` | Error reporting |
| `DEBUG` | `[FTP]` | Detailed debugging info |

### Example Log Output

```
[INFO] [FTP] New folder detected: Package_Batch_001
[INFO] [FOLDER] Start processing folder: Package_Batch_001
[INFO] [IMAGE] Processing image from folder 'Package_Batch_001'...
[INFO] [IMAGE] Extracted 2 stickers from folder 'Package_Batch_001'.
[INFO] [STICKER] Analyzing sticker 'Label_1' from folder 'Package_Batch_001'...
[INFO] [STICKER] Analysis returned 1 packages for 'Label_1'.
[INFO] [FOLDER] Completed processing folder 'Package_Batch_001'. Total packages: 3
```

## 🔄 Workflow Example

### Scenario: 2 New Folders Arrive

```
Folder A: 3 images
  └─ Image 1: 2 stickers
  └─ Image 2: 1 sticker
  └─ Image 3: 3 stickers

Folder B: 2 images
  └─ Image 1: 2 stickers
  └─ Image 2: 2 stickers
```

### Processing Timeline (Parallel)

```
Time 0s:  ┌─ Process Folder A ────────────────────────────┐
          │                                                │
          ├─ Image 1 → Extract 2 stickers → Analyze both ─┤
          ├─ Image 2 → Extract 1 sticker  → Analyze it   ─┤
          ├─ Image 3 → Extract 3 stickers → Analyze all  ─┤
          │                                                │
          └────────────────────────────────────────────────┘
          
          ┌─ Process Folder B ────────────────────────────┐
          │                                                │
          ├─ Image 1 → Extract 2 stickers → Analyze both ─┤
          ├─ Image 2 → Extract 2 stickers → Analyze both ─┤
          │                                                │
          └────────────────────────────────────────────────┘

Time ~2s: All complete! 🎉
          Total: 12 stickers analyzed
```

### Processing Timeline (Serial - OLD)

```
Time 0s:   Process Folder A
             → Image 1 → Sticker 1 → Sticker 2
Time 2s:     → Image 2 → Sticker 1
Time 3s:     → Image 3 → Sticker 1 → Sticker 2 → Sticker 3
Time 6s:   Process Folder B
             → Image 1 → Sticker 1 → Sticker 2
Time 8s:     → Image 2 → Sticker 1 → Sticker 2
Time 10s:  Complete

Total: 12 stickers analyzed (5x slower! 🐌)
```

## 🚀 Usage

### Service Registration

```csharp
services.AddHostedService<FtpPollingBackgroundService>();
services.AddScoped<IFtpPollingService, FtpPollingService>();
services.AddScoped<IPackagesAnalysisWorkflow, PackagesAnalysisWorkflow>();
services.AddScoped<ICompanyNameService, CompanyNameService>();
services.AddScoped<StickersExtractorService>();
```

### Configuration

Ensure your `appsettings.json` includes:

```json
{
  "FtpSettings": {
    "Host": "ftp.example.com",
    "Username": "your_username",
    "Password": "your_password",
    "PollingIntervalSeconds": 5
  },
  "CompanySettings": {
    "DeliveryCompanyName": "IsraelPostOffice"
  }
}
```

## 🧪 Testing Considerations

### Unit Testing

Each method can be tested independently:

```csharp
[Fact]
public async Task ProcessSingleImageAsync_NoStickers_ReturnsEmptyList()
{
    // Arrange
    var mockExtractor = Mock<StickersExtractorService>();
    mockExtractor.Setup(x => x.DetectStickers(It.IsAny<byte[]>(), It.IsAny<string>()))
                 .Returns(new List<DetectionResponse>());
    
    // Act
    var result = await service.ProcessSingleImageAsync(image, "folder1", "CompanyName");
    
    // Assert
    Assert.Empty(result);
}
```

### Integration Testing

Test the full workflow with a mock FTP server:

```csharp
[Fact]
public async Task PollAndProcessFoldersAsync_NewFolder_ProcessesSuccessfully()
{
    // Arrange
    mockFtpService.Setup(x => x.GetCurrentFoldersFromFtpAsync())
                  .ReturnsAsync(new[] { "folder1" });
    
    // Act
    await service.PollAndProcessFoldersAsync(CancellationToken.None);
    
    // Assert
    mockFtpService.Verify(x => x.DownloadFolderFromFtpAsync("folder1"), Times.Once);
}
```

## ⚙️ Configuration Options

### Polling Interval
Adjust the delay between polling cycles:

```csharp
await Task.Delay(5000, stoppingToken); // Change 5000 to desired milliseconds
```

### Parallelism Control
To limit concurrent operations (for resource-constrained environments):

```csharp
// Use SemaphoreSlim to limit concurrent tasks
private readonly SemaphoreSlim _maxParallelism = new SemaphoreSlim(10);

// In processing method:
await _maxParallelism.WaitAsync();
try
{
    // Process
}
finally
{
    _maxParallelism.Release();
}
```

## 📈 Monitoring & Metrics

### Key Metrics to Track

- **Folders processed per minute**
- **Average processing time per folder**
- **Stickers detected per image**
- **AI analysis success rate**
- **Error rate by stage**

### Recommended Tools

- **Application Insights** - For cloud deployments
- **Prometheus + Grafana** - For on-premise monitoring
- **Serilog** - For structured logging

## 🔐 Security Considerations

1. **FTP Credentials**: Store in secure configuration (Azure Key Vault, AWS Secrets Manager)
2. **Image Data**: Ensure images are deleted after processing
3. **Error Logging**: Avoid logging sensitive package information
4. **Rate Limiting**: Consider implementing rate limits for AI API calls

## 🐛 Troubleshooting

### Common Issues

#### Service not processing folders
- Check FTP connectivity
- Verify `_knownFolders` isn't preventing re-processing
- Check logs for exceptions

#### Slow performance
- Verify parallel processing is enabled
- Check system resources (CPU, memory, network)
- Review AI API rate limits

#### Memory issues
- Implement image disposal after processing
- Add batching limits for large folders
- Monitor memory usage over time

## 📝 License

[Your License Here]

## 👥 Contributors

[Your Team/Name Here]

---

**Last Updated:** February 2026  
**Version:** 2.0 (Parallel Processing Edition)