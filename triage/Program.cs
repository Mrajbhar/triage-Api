using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using triage.Data;
using triage.Entities;
using triage.Hubs;
using triage.Services;
using triage.Workers;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

        if (!builder.Environment.IsDevelopment()) return;

        var exception = ctx.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is null) return;

        ctx.ProblemDetails.Detail = exception.Message;
        ctx.ProblemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
        ctx.ProblemDetails.Extensions["stackTrace"] = exception.ToString();
    };
});


builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0; 
    });
});


builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("database");

//builder.Services.AddDbContext<AppDbContext>(opt =>
//    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<AppDbContext>(opt =>
opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITicketEventService, TicketEventService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();


var redisConn = builder.Configuration.GetConnectionString("Redis");
var signalR = builder.Services.AddSignalR();

if (!string.IsNullOrWhiteSpace(redisConn))
{
    signalR.AddStackExchangeRedis(redisConn);          
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
}
else
{
    builder.Services.AddDistributedMemoryCache();      
}

builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<ICacheService, CacheService>();

builder.Services.AddHostedService<SlaEscalationWorker>();

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
                //policy.WithOrigins("http://localhost:5173")
                policy.WithOrigins("https://triage-api-h80s.onrender.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());     
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();       
    app.UseSwaggerUI();     
}

app.UseCors("AllowReact");

app.UseAuthentication();     
app.UseAuthorization();

app.UseRateLimiter();       

app.MapControllers();
app.MapHub<TicketsHub>("/hubs/tickets");
app.MapHealthChecks("/health");  
app.Run();