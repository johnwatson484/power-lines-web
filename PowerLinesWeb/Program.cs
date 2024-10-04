using PowerLinesWeb.Accuracy;
using PowerLinesWeb.Fixtures;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FixtureOptions>(builder.Configuration.GetSection(key: "FixtureUrl"));
builder.Services.Configure<AccuracyOptions>(builder.Configuration.GetSection(key: "AccuracyUrl"));

builder.Services.AddScoped<IFixtureApi, FixtureApi>();
builder.Services.AddScoped<IAccuracyApi, AccuracyApi>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.MapControllers();
app.UseStaticFiles();

await app.RunAsync();
