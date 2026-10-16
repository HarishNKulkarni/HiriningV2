//using DinkToPdf;
//using DinkToPdf.Contracts;

//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.FileProviders;
//using Microsoft.Extensions.FileProviders;
//using Recruitment.Data;



//var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();

//builder.Services.AddSwaggerGen(c =>
//{
//    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

//});
//builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration["Data:ApplicationDB:ConnectionString"]
//    ));
//builder.Services.AddHttpClient("EP", client =>
//{
//    client.Timeout = TimeSpan.FromSeconds(30);
//});
//var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
//    app.UseDeveloperExceptionPage();
//}

//app.UseSwagger();

//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
//    c.RoutePrefix = string.Empty;
//});

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();



/////mwb server 192.168.0.197
/////

using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Recruitment.Data;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // The IIS application pool identity needs write permission
    // on this logs folder.
    var logPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "logs");

    Directory.CreateDirectory(logPath);

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override(
            "Microsoft.AspNetCore",
            Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logPath, "app-log-.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true)
        .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting Recruitment API");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSingleton<IConverter>(
        new SynchronizedConverter(new PdfTools()));

    // Registers IHttpClientFactory for your EP integration APIs.
    // Must be registered before builder.Build().
    builder.Services.AddHttpClient("EP", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Preserved for your testing environment.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var connectionString =
        builder.Configuration["Data:ApplicationDB:ConnectionString"];

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Missing Data:ApplicationDB:ConnectionString in appsettings.json.");
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    var app = builder.Build();

    // Supports requests prefixed with /HiringV2.
    // IIS also supplies PathBase when hosted as a /HiringV2 application.
    app.UsePathBase("/HiringV2");

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    // Enable when your testing site has a working HTTPS binding.
    // app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();

    // Serve public files from wwwroot.
    app.UseStaticFiles();

    // Serve public files from Content only when that folder exists.
    var contentPath = Path.Combine(
        app.Environment.ContentRootPath,
        "Content");

    if (Directory.Exists(contentPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(contentPath),
            RequestPath = "/Content"
        });
    }
    else
    {
        Log.Warning(
            "Content folder not found at {ContentPath}; static mapping skipped",
            contentPath);
    }

    // Enabled on the testing server, including Production environment.
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "./v1/swagger.json",
            "Recruitment API V1");

        options.RoutePrefix = "swagger";
    });

    app.UseRouting();
    app.UseCors("AllowAll");
    app.UseAuthorization();

    app.MapControllers();

    // Open Swagger when visiting this application's root.
    app.MapGet("/", (HttpContext context) =>
        Results.Redirect(
            $"{context.Request.PathBase}/swagger/index.html"))
        .ExcludeFromDescription();

    Log.Information("Recruitment API configured successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Recruitment API failed to start");

    // Also report failures that occur before Serilog is initialized.
    Console.Error.WriteLine(ex);

    throw;
}
finally
{
    Log.CloseAndFlush();
}