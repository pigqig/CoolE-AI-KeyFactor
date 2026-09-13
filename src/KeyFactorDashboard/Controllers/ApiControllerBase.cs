using KeyFactorDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeyFactorDashboard.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult FromPython(PythonApiException ex)
    {
        var details = new ProblemDetails
        {
            Status = ex.StatusCode,
            Title = ex.ErrorCode,
            Detail = ex.Message,
            Type = $"https://httpstatuses.com/{ex.StatusCode}"
        };
        details.Extensions["errorCode"] = ex.ErrorCode;
        return new ObjectResult(details) { StatusCode = ex.StatusCode };
    }
}
