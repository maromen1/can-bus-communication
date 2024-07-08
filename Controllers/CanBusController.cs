using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

[ApiController]
[Route("[controller]")]
public class CanBusController : ControllerBase
{
    private readonly ILogger<CanBusController> _logger;

    public CanBusController(ILogger<CanBusController> logger)
    {
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCanBusLog()
    {
        try
        {
            _logger.LogInformation("ProcessCanBusLog method called");

            var file = Request.Form.Files[0];
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded");
                return BadRequest("No file uploaded.");
            }

            _logger.LogInformation($"File received: {file.FileName}, Size: {file.Length} bytes");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileData = memoryStream.ToArray();

            _logger.LogInformation($"File data length: {fileData.Length} bytes");

            var transferData = CanBusParser.ProcessCanBusLog(fileData);

            _logger.LogInformation($"Processed data length: {transferData.Length} bytes");

            if (transferData.Length == 0)
            {
                _logger.LogWarning("Processed data is empty");
                return BadRequest("No transfer data extracted from the file.");
            }

            return File(transferData, "application/octet-stream", "transferdata.bin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CAN bus log");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}