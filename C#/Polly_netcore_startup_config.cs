
// ****** Begin: Swagger Config **********
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var request = new HttpContextAccessor().HttpContext?.Request;
    if (request != null)
    {
        var scheme = request.Host.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
        var urlBuilder = new UriBuilder(scheme, request.Host.Host, request.Host.Port ?? -1);
        var templatesUrl = urlBuilder.Uri.AbsoluteUri + "templates";
        var errorMappingUrl = urlBuilder.Uri.AbsoluteUri + "errormapping";
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Profile API",
            Description = $"Access to customer profile data for the CXP. {(new string[] { "Development", "dev" }.Contains(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) ? "View and edit message templates at [" + templatesUrl + "](" + templatesUrl + ")<br>View all the error codes and their mapping at [" + errorMappingUrl + "](" + errorMappingUrl + ")" : string.Empty)}"
        });
    }

    #region Swagger Auth
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    #endregion

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// More code .....

// disable swagger in prod
if (!app.Environment.IsProduction())
{
    app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swaggerDoc, httpRequest) =>
        {
            if (!app.Environment.IsDevelopment())
            {
                swaggerDoc.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"https://{httpRequest.Host.Value}/cxp-profile-api" } };
            }
        });
    });
    app.UseSwaggerUI();
}
// ****** End: Swagger Config **********

// ****** Begin: HealthCheck Config **********
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CXPDBContext>("healthcheck_db",
        tags: new[] { "db" },
        customTestQuery: (a, b) =>
        {
            return a.Lookups.AnyAsync(p => p.LookupId > 0);
        });
// ****** End: HealthCheck Config **********

// ****** Begin: Register Action filters and Json options **********
// Register ActionFilter with .AddControllers
builder.Services.AddScoped<ActionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ActionFilter>();
    options.EnableEndpointRouting = false; // needed for web app controllers (e.g. TemplatesController)
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 2;
    })
    .AddNewtonsoftJson(options => { });

builder.Services.AddCors(options =>
{
    var CORS_Origins = Environment.GetEnvironmentVariable("CORS_Origins");
    CORS_Origins = string.IsNullOrEmpty(CORS_Origins) ? builder.Configuration.GetValue<string>("CORS:Origins") : CORS_Origins;

    options.AddPolicy(name: CORS_AllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins(CORS_Origins.Split(","))
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                      });
});

// More code....

app.UseCors(CORS_AllowSpecificOrigins);
// ****** End: Register Action filters, Json options, and CORS **********

// ****** Begin: Dev env configs **********
if (app.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;

    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
// ****** End: Dev env configs **********

// ****** Begin: Application Insights configs **********
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddApplicationInsightsKubernetesEnricher(); // Add this line if running in Kubernetes, e.g. AKS
builder.Services.AddSingleton<ITelemetryInitializer, CustomPropertiesTelemetryInitializer>(); // See ApplicationInsightsMiddleware.cs for implemented Initializer 

// Enable custom telemetry for HTTP request dependencies in application insights
if (enableDetailedData)
{
    // Middleware classes defined in ApplicationInsightsMiddleware.cs file in the root folder	
    builder.Services.AddSingleton<ITelemetryInitializer, DependencyTelemetryInitializer>();
}

// Add App Insights to the list of ILogger providers
builder.Logging.AddApplicationInsights();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("CXP-renewal-api", LogLevel.Warning);

// More code....

// Enable custom telemetry for HTTP requests in application insights
if (enableDetailedData)
{
    // This block is necessary for the app insights telemetry middleware below to work
    app.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();
        await next();
    });

    // Middleware classes defined in ApplicationInsightsMiddleware.cs file in the root folder	
    app.UseMiddleware<RequestTelemetryMiddleware>();
    app.UseMiddleware<ResponseTelemetryMiddleware>();
}
// ****** End: Application Insights configs **********
