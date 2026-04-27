using Microsoft.EntityFrameworkCore;
using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Analysis;
using PowerLinesWeb.Data;
using PowerLinesWeb.Fixtures;
using PowerLinesWeb.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MessageOptions>(builder.Configuration.GetSection(key: "Message"));
builder.Services.Configure<ThresholdOptions>(builder.Configuration.GetSection(key: "Threshold"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PowerLinesWeb"), options =>
        options.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

builder.Services.AddScoped<IFixtureService, FixtureService>();
builder.Services.AddScoped<IAccuracyService, AccuracyService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

builder.Services.AddHostedService<MessageService>();
builder.Services.AddHostedService<FixtureAnalysisBackgroundService>();
builder.Services.AddHostedService<ResultAnalysisBackgroundService>();
builder.Services.AddHostedService<AccuracyBackgroundService>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(360);
});

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", string.Join(" ",
        "font-src 'self' fonts.gstatic.com *.cloudflare.com *.fontawesome.com;",
        "img-src 'self' *.google.com;",
        "script-src 'self' 'unsafe-inline' code.jquery.com cdnjs.cloudflare.com *.bootstrapcdn.com *.fontawesome.com *.googletagmanager.com *.google.com;",
        "style-src 'self' 'unsafe-inline' fonts.googleapis.com *.bootstrapcdn.com cdnjs.cloudflare.com code.jquery.com;",
        "connect-src 'self' *.fontawesome.com;",
        "frame-ancestors 'self';",
        "form-action 'self';"
    ));
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-site");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), geolocation=(), magnetometer=(), microphone=(), payment=(), usb=()");
    await next();
});

app.UseHttpsRedirection();
app.MapControllers();
app.UseStaticFiles();

ApplyMigrations(app.Services);

await app.RunAsync();

static void ApplyMigrations(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
