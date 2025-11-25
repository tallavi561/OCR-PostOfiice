using Microsoft.AspNetCore.Mvc;
using CameraAnalyzer.bl.Utils;

using CameraAnalyzer.bl.Services.PackagesAnalysis.WorkFlow;

namespace CameraAnalyzer.Controllers
{
    [ApiController]
    [Route("api1/v1/[controller]")]
    public class CameraAnalyzerController : ControllerBase
    {
        private readonly IPackagesAnalysisWorkflow _PackagesAnalysisWorkflow;

        public CameraAnalyzerController(IPackagesAnalysisWorkflow PackagesAnalysisWorkflow)
        {
            _PackagesAnalysisWorkflow = PackagesAnalysisWorkflow;
        }

        [HttpGet("getHomePage")]
        public IActionResult GetHomePage()
        {
            Logger.LogInfo("Home page accessed.");
            return Ok("HELLO WORLD");
        }

        [HttpGet("startProcess")]
        public async Task<IActionResult> StartProcess()
        {
            Logger.LogInfo("Start process is starting.");
            var result = await _PackagesAnalysisWorkflow.AnalyzeImageAsync();
            return Ok(result);
        }
    }
}