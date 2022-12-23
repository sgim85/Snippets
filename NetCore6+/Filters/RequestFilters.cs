// Filters in ASP.NET Core allow code to run before or after specific stages in the request processing pipeline.
// More on filters in asp.net core: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-7.0

// This Section focuses on the ModelState validation filters

// The default "ModelStateInvalidFilter" filter is implicitly added to all types and actions annotated with the [ApiController] attribute.
// So most validation purpose you do not need any additional logic for returing validation errors to clients.

// If you would like to apply additional filter rules to the request pipeline, you can write a custom filter like below:

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Profile.API
{
    /// <summary>
    /// Global validation filter for the ModelState before executing controller action.
    /// Note: This is overriden by the default (and implicit) ModelStateInvalidFilter if the Controller is decorated with the [ApiController] attribute
    /// </summary>
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid == false)
            {
                context.Result = new BadRequestObjectResult(context.ModelState); // Set bad request response
            }
        }
    }
}

// Then register it in program.cs or start.cs
builder.Services.AddControllers(options =>
{
    // Add a data validation filter for the http request models
    options.Filters.Add<ValidateModelAttribute>();
});