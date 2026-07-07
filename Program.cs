using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.ResponseCompression;
using NathanPortfolio.CustomServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddHttpClient("OpenRouter");
builder.Services.AddSingleton<IOpenRouterService, OpenRouterService>();
builder.Services.AddHttpClient("GitHub");
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddHttpClient("Itch");
builder.Services.AddSingleton<IItchService, ItchService>();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.SmallestSize;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.SmallestSize;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items["csp-nonce"] = nonce;

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    // style-src needs 'unsafe-inline' for the Projects page's per-repo language-color
    // swatch (style="background:...") - script-src stays nonce-only, which is the
    // directive that actually matters for blocking injected/XSS script execution.
    context.Response.Headers["Content-Security-Policy"] =
        $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' https://img.itch.zone; font-src 'self'; connect-src 'self'; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var oneYear = (60 * 60 * 24 * 365).ToString();
        var oneDay = (60 * 60 * 24).ToString();
        var isVersioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = isVersioned
            ? $"public,max-age={oneYear},immutable"
            : $"public,max-age={oneDay}";
    }
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/api/status", () =>
{
    var informationalVersion = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;

    var plusIndex = informationalVersion?.IndexOf('+') ?? -1;
    var commit = plusIndex >= 0
        ? informationalVersion![(plusIndex + 1)..][..Math.Min(7, informationalVersion.Length - plusIndex - 1)]
        : null;

    return Results.Json(new
    {
        status = "operational",
        serverTimeUtc = DateTime.UtcNow,
        startedAtUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
        commit
    });
});

app.Run();
