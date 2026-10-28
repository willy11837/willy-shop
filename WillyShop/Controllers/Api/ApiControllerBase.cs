using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;

namespace WillyShop.Controllers.Api;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ApiSuccess<T>(
        T? data,
        string message = "操作成功",
        int statusCode = StatusCodes.Status200OK)
        => StatusCode(statusCode, new ApiResponse<T>(true, statusCode, message, data));

    protected ObjectResult ApiFailure(string message, int statusCode)
        => StatusCode(statusCode, new ApiResponse<object?>(false, statusCode, message, null));

    protected ObjectResult ApiValidationFailure(
        IReadOnlyDictionary<string, string[]> errors,
        string? message = null)
        => StatusCode(
            StatusCodes.Status400BadRequest,
            new ApiResponse<ApiValidationErrorData>(
                false,
                StatusCodes.Status400BadRequest,
                message ?? string.Join("、", errors.Values.SelectMany(values => values).Distinct()),
                new ApiValidationErrorData(errors)));
}