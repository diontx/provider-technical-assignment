using Candidate.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Candidate.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Maps an expected business failure to an RFC 7807 problem response.</summary>
    protected ActionResult ToProblem(ServiceError error)
    {
        switch (error.Kind)
        {
            case ServiceErrorKind.NotFound:
                return Problem(title: "Not found", detail: error.Message, statusCode: StatusCodes.Status404NotFound);

            case ServiceErrorKind.Conflict:
                return Problem(title: "Conflict", detail: error.Message, statusCode: StatusCodes.Status409Conflict);

            default:
                foreach (var (field, messages) in error.FieldErrors ?? new Dictionary<string, string[]>())
                {
                    foreach (var message in messages)
                    {
                        ModelState.AddModelError(field, message);
                    }
                }

                return ValidationProblem(ModelState);
        }
    }
}
