using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Fixtures;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FixtureOptions>(builder.Configuration.GetSection(key: "FixtureUrl"));
builder.Services.Configure<AccuracyOptions>(builder.Configuration.GetSection(key: "AccuracyUrl"));

builder.Services.AddScoped<IFixtureApi, FixtureApi>();
builder.Services.AddScoped<IAccuracyApi, AccuracyApi>();

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

await app.RunAsync();
