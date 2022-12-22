// In program.cs (or start.cs), register the Authentication service and set the Authentication and Challenge schemes to JwtBearerDefaults.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

// In program.cs (or start.cs), register the JWTMiddleware that will be invoked as part of the Http Request pipeline
app.UseMiddleware<JwtMiddleware>();

// In program.cs (or start.cs), register the JWTManager class used by JWTMiddleware for validating JWT tokens
builder.Services.AddSingleton<JwtManager, JwtManager>();

// Optional:In program.cs (or start.cs), add Security definitions for the Swagger page
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Profile API",
        Description = "Access to customer profile data",
    });

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