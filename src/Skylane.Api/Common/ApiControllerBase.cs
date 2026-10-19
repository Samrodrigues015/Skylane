using Microsoft.AspNetCore.Mvc;

namespace Skylane.Api.Common;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Converte um erro de negócio numa resposta ProblemDetails (RFC 9457),
    /// com o código do erro em "code" para o front-end poder reagir a ele.
    /// </summary>
    protected ObjectResult ToProblem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Gone => StatusCodes.Status410Gone,
            _ => StatusCodes.Status500InternalServerError
        };

        var result = Problem(title: error.Message, statusCode: statusCode);

        if (result.Value is ProblemDetails details)
        {
            details.Extensions["code"] = error.Code;
        }

        return result;
    }
}
