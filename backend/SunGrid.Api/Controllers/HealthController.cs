// File name: HealthController.cs
// Project name: SunGrid
// Purpose of the file: Public API endpoint to verify application health status.
// Author placeholder: SunGrid Development Team

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SunGrid.Api.DTOs;

namespace SunGrid.Api.Controllers
{
    /// <summary>
    /// Controller serving basic system health checks.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Returns system health status, application name, and UTC timestamp.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
        public IActionResult GetHealth()
        {
            var response = new HealthResponse
            {
                Status = "Healthy",
                Message = "API is running",
                Application = "SunGrid",
                TimestampUtc = DateTime.UtcNow
            };

            return Ok(response);
        }
    }
}
