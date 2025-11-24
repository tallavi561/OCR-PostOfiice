# # Path to your project - change this if needed
# $projectPath = "C:\Users\user\OneDrive\Desktop\my_projects\BibleSearch\backend-c#\v3\CameraAnalyzer"

# # Change directory to the project folder
# Set-Location $projectPath

Write-Host "Cleaning previous build..."
dotnet clean

Write-Host "Starting dotnet build..."
$buildResult = dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded. Running the application..."
    dotnet run
} else {
    Write-Host "Build failed. Please fix errors before running."
}
