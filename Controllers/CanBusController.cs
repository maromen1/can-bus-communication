using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

[ApiController]
[Route("[controller]")]
public class CanBusController : ControllerBase
{
    [HttpPost("process")]
    public async Task<IActionResult> ProcessCanBusLog()
    {
        try
        {
            var file = Request.Form.Files[0];
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileData = memoryStream.ToArray();

            var transferData = CanBusParser.ProcessCanBusLog(fileData);

            return File(transferData, "application/octet-stream", "transferdata.bin");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}