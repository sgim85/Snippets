// ********************** BEGIN: standardize all responses by wrapping them in a common response class (ResponsePayload.cs) **********************
public class ActionFilter : IActionFilter
{
    /// <summary>
    /// Wrap action result in a ResponsePayload object: {data, Message, statusCode}
    /// </summary>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // TemplatesController, ErrorMapping is a web app (website) controller. Do not intercept it.
        var controller = context?.Controller?.ToString();
        if (controller != null && (controller.Contains("TemplatesController", StringComparison.OrdinalIgnoreCase) || controller.Contains("ErrorMappingController", StringComparison.OrdinalIgnoreCase)))
            return;

        // ObjectResult: Result contains both statuscode and data. E.g. return Ok(data)
        var objectResult = context.Result as ObjectResult;
        if (objectResult != null)
        {
            var errResp = objectResult.Value as ErrorResponse;

            if (objectResult.StatusCode == (int?)HttpStatusCode.OK && errResp == null)
            {
                objectResult.Value = new ResponsePayload
                {
                    Data = objectResult.Value,
                    StatusCode = objectResult.StatusCode
                };
            }
            else
            {
                objectResult.Value = new ResponsePayload
                {
                    //Data = errResp == null && objectResult.Value != null ? objectResult.Value : null,
                    Data = errResp != null ? errResp.data : objectResult.Value,
                    Message = errResp != null ? errResp.ErrorMessage : null,
                    StatusCode = objectResult.StatusCode,
                    // CatchAll indicates props non-specific error that has no wellknown MessageCode
                    ErrorCodes = new List<string> { errResp != null ? errResp.ErrorCode : RegexErrorCodes.CatchAll }
                };
            }
        }
        else
        {
            // If file download, pass through. Do not interrupt.
            var fcr = context.Result as FileContentResult;
            if (fcr != null)
            {
                //.....
            }
            else
            {
                // StatusCodeResult = Result contains only the statuscode, no additional data. E.g. return Ok()
                var scr = context.Result as StatusCodeResult;
                var _statusCode = (scr != null ? scr.StatusCode : context.HttpContext.Response.StatusCode);
                var result = new ObjectResult(new ResponsePayload
                {
                    StatusCode = _statusCode,
                    ErrorCodes = (_statusCode < 200 || _statusCode > 200) ? new List<string> { RegexErrorCodes.CatchAll } : new List<string>()
                });
                result.StatusCode = _statusCode;
                context.Result = result;
            }
        }
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {

    }
}

public static class InvalidModelStateResponse
{
    /// <summary>
    /// Intercepts an invalid ModelState Response and wraps it in a ResponsePayload (like all other responses)
    /// </summary>
    public static Func<ActionContext, IActionResult> InvalidResponseFactory = (actionContext) =>
    {
        return new BadRequestObjectResult(new ResponsePayload
        {
            StatusCode = 400,
            ErrorCodes = actionContext.ModelState.Values
                            .Where(m => m.ValidationState == ModelValidationState.Invalid)
                            .SelectMany(m => m.Errors)
                            .Select(n => n.ErrorMessage)
                            .ToList()
        });
    };
}

/// <summary>
/// Standard response object.
/// </summary>
public class ResponsePayload
{
    public dynamic Data { get; set; } = null!;
    public int? StatusCode { get; set; }
    public List<string> ErrorCodes { get; set; } = new List<string>();
    public dynamic Message { get; set; } = null!;
}

/// <summary>
/// Response payload for non 200 responses.
/// </summary>
public class ErrorResponse
{
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public dynamic data { get; set; }

    public static string GetDescription<T>(string errorCode)
    {
        var desc = (from field in typeof(T).GetFields()
                    where field != null && (string)field.GetValue(null) == errorCode && field.GetCustomAttribute<DescriptionAttribute>() != null
                    select field.GetCustomAttribute<DescriptionAttribute>()).FirstOrDefault();
        return desc != null ? desc.Description : null;
    }
}
// ********************** END: standardize all responses by wrapping them in a common response class (ResponsePayload.cs) **********************
