using System.Diagnostics;
using System.Reflection;
using NathanPortfolio.CustomServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddHttpClient("OpenRouter");
builder.Services.AddSingleton<IOpenRouterService, OpenRouterService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

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
