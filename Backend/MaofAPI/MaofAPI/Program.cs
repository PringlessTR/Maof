using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MaofAPI.Data;
using MaofAPI.Hubs;
using System.Net;
using System.Text;
using System.Text.Json;
using MaofAPI.Authorization;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Configure OpenAPI/Swagger
builder.Services.AddOpenApi();

// Configure CORS for React Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtConfig = builder.Configuration.GetSection("JWT");
    
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidAudience = jwtConfig["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"] ?? throw new InvalidOperationException("JWT Key is not configured")))
    };
    
    // Configure SignalR to use JWT
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Register the Permission handlers
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AdminPermissionHandler>();

// Add authorization with policies for permissions
builder.Services.AddAuthorization(options =>
{
    // Add a policy for each permission
    // Products permissions
    options.AddPolicy(Permissions.ViewProducts, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewProducts)));
    options.AddPolicy(Permissions.CreateProducts, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.CreateProducts)));
    options.AddPolicy(Permissions.EditProducts, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditProducts)));
    options.AddPolicy(Permissions.DeleteProducts, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DeleteProducts)));
    options.AddPolicy(Permissions.ManageStock, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManageStock)));
    
    // Categories permissions
    options.AddPolicy(Permissions.ViewCategories, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewCategories)));
    options.AddPolicy(Permissions.CreateCategories, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.CreateCategories)));
    options.AddPolicy(Permissions.EditCategories, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditCategories)));
    options.AddPolicy(Permissions.DeleteCategories, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DeleteCategories)));
    
    // Sales permissions
    options.AddPolicy(Permissions.ViewSales, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewSales)));
    options.AddPolicy(Permissions.CreateSales, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.CreateSales)));
    options.AddPolicy(Permissions.EditSales, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditSales)));
    options.AddPolicy(Permissions.DeleteSales, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DeleteSales)));
    options.AddPolicy(Permissions.DiscountSales, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DiscountSales)));
    
    // Promotions permissions
    options.AddPolicy(Permissions.ViewPromotions, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewPromotions)));
    options.AddPolicy(Permissions.CreatePromotions, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.CreatePromotions)));
    options.AddPolicy(Permissions.EditPromotions, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditPromotions)));
    options.AddPolicy(Permissions.DeletePromotions, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DeletePromotions)));
    options.AddPolicy(Permissions.ManagePromotions, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManagePromotions)));
    
    // Users permissions
    options.AddPolicy(Permissions.ViewUsers, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewUsers)));
    options.AddPolicy(Permissions.CreateUsers, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.CreateUsers)));
    options.AddPolicy(Permissions.EditUsers, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditUsers)));
    options.AddPolicy(Permissions.DeleteUsers, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.DeleteUsers)));
    options.AddPolicy(Permissions.ManageUserRoles, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManageUserRoles)));
    options.AddPolicy(Permissions.ManageRoles, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManageRoles)));
    
    // Store settings
    options.AddPolicy(Permissions.ViewStoreSettings, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewStoreSettings)));
    options.AddPolicy(Permissions.EditStoreSettings, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.EditStoreSettings)));
    
    // System permissions
    options.AddPolicy(Permissions.SyncData, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.SyncData)));
    options.AddPolicy(Permissions.ViewLogs, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewLogs)));
    
    // Reports permissions
    options.AddPolicy(Permissions.ViewReports, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ViewReports)));
    options.AddPolicy(Permissions.ExportReports, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ExportReports)));
    
    // Admin permissions
    options.AddPolicy(Permissions.ManageAllStores, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManageAllStores)));
    options.AddPolicy(Permissions.ManageSystemSettings, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.ManageSystemSettings)));
    options.AddPolicy(Permissions.SystemSettings, 
        policy => policy.Requirements.Add(new PermissionRequirement(Permissions.SystemSettings)));
});

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100 KB
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Maof API", Version = "v1" });
    
    // JWT Authentication support
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Add Services for Controllers
builder.Services.AddScoped<MaofAPI.Services.AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Maof API v1"));
}
else
{
    // Global exception handler for production
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            
            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            
            var errorResponse = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "An unexpected error occurred.",
                DetailedMessage = app.Environment.IsDevelopment() ? exception?.Message : null
            };
            
            await context.Response.WriteAsJsonAsync(errorResponse);
        });
    });
    
    // HSTS - HTTP Strict Transport Security Protocol
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

// Authentication should be before authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<SyncHub>("/hubs/sync");

app.Run();
