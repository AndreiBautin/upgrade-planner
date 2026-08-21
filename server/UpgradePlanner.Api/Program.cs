using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Configuration;
using UpgradePlanner.Api.Data;
using UpgradePlanner.Api.Services;

// ---------------------------------------------------------------------------
// Composition root. Every dependency in the application is constructed here and
// nowhere else, so "what depends on what" is answerable by reading one file.
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// Configuration is resolved once, up front, into an immutable record. Parsing
// never throws: bad input becomes a documented default plus a warning, collected
// here and logged once the logger exists.
var configWarnings = new List<string>();
var options = AppOptions.Parse(builder.Configuration, configWarnings);
builder.Services.AddSingleton(options);

// Most PaaS hosts assign a port at runtime via $PORT and expect the app to bind
// to it directly rather than using launchSettings.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// The API accepts unauthenticated writes from anyone (see docs/SECURITY.md).
// Kestrel's 30 MB default body limit is far more room than a JSON upgrade needs,
// and on a host with an ephemeral disk it is a disk-exhaustion vector.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(db => db.UseSqlite(options.ConnectionString));
builder.Services.AddScoped<UpgradeService>();

// Rate limiting is real protection rather than decoration: with no
// authentication, it is the only thing bounding how fast one client can write.
// A fixed window is chosen over a sliding one because it is trivial to explain
// and the precision difference is irrelevant at this scale.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Behind a proxy the socket address is the proxy's, so prefer the
            // forwarded client address when one is present. This is a fairness
            // control, not a security control - a header can be spoofed, and
            // there is nothing behind it worth protecting with one.
            partitionKey: context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

const string ClientCorsPolicy = "Client";
builder.Services.AddCors(cors => cors.AddPolicy(ClientCorsPolicy, policy =>
    policy.WithOrigins([.. options.AllowedOrigins])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

foreach (var warning in configWarnings)
{
    logger.LogWarning("Configuration warning: {Warning}", warning);
}

// Log event names and scalars only, never user content. Nothing here can print
// the title of an upgrade, so these logs are safe to leave on in production.
logger.LogInformation(
    "Configuration resolved. DemoMode={DemoMode} AllowedOriginCount={OriginCount} Build={BuildSha} Environment={Environment}",
    options.DemoMode, options.AllowedOrigins.Count, options.BuildSha, app.Environment.EnvironmentName);

if (!options.DemoMode && app.Environment.IsProduction()
    && options.AllowedOrigins is [AppOptions.DefaultAllowedOrigin])
{
    logger.LogWarning(
        "ALLOWED_ORIGINS is unset in Production, so only localhost may call this API. "
        + "Browsers on the deployed frontend will be blocked.");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Two separate operations, never one call with a flag.
    //
    // The destructive one is reachable only from an explicit command line
    // argument, and only in demo mode - which forces the connection string to
    // demo.db regardless of configuration, so it cannot run against real data.
    if (args.Contains("--reset-demo-data"))
    {
        if (!options.DemoMode)
        {
            logger.LogError("--reset-demo-data refused: DEMO_MODE is not enabled. Refusing to delete data.");
            return 1;
        }

        var reset = DemoSeeder.ResetToDemoData(db, DateTime.UtcNow);
        logger.LogInformation("Demo data reset. RowsWritten={RowsWritten}", reset);
        return 0;
    }

    if (options.DemoMode)
    {
        var seeded = DemoSeeder.SeedIfEmpty(db, DateTime.UtcNow);
        logger.LogInformation(
            "Demo seed complete. RowsWritten={RowsWritten} TotalRows={TotalRows}",
            seeded, DemoSeeder.CountUpgrades(db));
    }
}

// Unhandled exceptions become a ProblemDetails body. ASP.NET Core already
// withholds stack traces outside Development, so this is about giving the client
// something parseable to show rather than about hiding internals.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    // Kestrel reports a request that broke a protocol limit - most relevantly the
    // 64 KB body cap above - as a typed BadHttpRequestException carrying the
    // right status code. Reporting that as 500 would blame the server for the
    // client's oversized request, and would tell the caller nothing actionable.
    var (status, title) = error is BadHttpRequestException badRequest
        ? (badRequest.StatusCode, "The request could not be processed.")
        : (StatusCodes.Status500InternalServerError, "An unexpected error occurred.");

    if (status >= StatusCodes.Status500InternalServerError)
    {
        app.Logger.LogError(error, "Unhandled exception. Method={Method} Path={Path}",
            context.Request.Method, context.Request.Path.Value);
    }
    else
    {
        app.Logger.LogWarning("Rejected request. Status={Status} Method={Method} Path={Path}",
            status, context.Request.Method, context.Request.Path.Value);
    }

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = app.Environment.IsDevelopment() ? error?.ToString() : null,
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Only redirect to HTTPS locally. In production the app sits behind a
    // TLS-terminating proxy that forwards plain HTTP on the internal hop, so
    // redirecting there would bounce the request in a loop.
    app.UseHttpsRedirection();
}

app.UseCors(ClientCorsPolicy);
app.UseRateLimiter();

// There is deliberately no app.UseAuthorization() here. The application has no
// authentication scheme and no [Authorize] attribute, so that middleware could
// not deny a single request - it would look like an access control while being
// incapable of acting as one. The absence is documented in docs/SECURITY.md.

app.MapControllers();

// Readiness probe, and the link between a running instance and a commit.
// Exempt from rate limiting so a host's health checks cannot throttle themselves.
app.MapGet("/health", (AppOptions opts) => Results.Ok(new
{
    status = "ok",
    demoMode = opts.DemoMode,
    build = opts.BuildSha,
    utc = DateTime.UtcNow,
})).DisableRateLimiting();

app.Run();
return 0;
