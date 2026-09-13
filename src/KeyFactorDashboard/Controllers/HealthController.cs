using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyFactorDashboard.Controllers;

/// <summary>Service health for the ASP.NET host and the Python analysis process.</summary>
[ApiController]
[Route("api/v1/health")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly IPythonApiClient _python;

    public HealthController(IPythonApiClient python) => _python = python;

    /// <summary>Returns host status and whether the sklearn service is reachable.</summary>
    /// <response code="200">Always returned; inspect the <c>python</c> field.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var pythonUp = await _python.HealthAsync(cancellationToken);
        return Ok(new HealthResponse
        {
            Status = "ok",
            Python = pythonUp ? "up" : "down"
        });
    }
}
