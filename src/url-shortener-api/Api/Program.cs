using Microsoft.AspNetCore.Authentication;
using UrlShortener.Api;
using UrlShortener.Application;
using UrlShortener.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services
    .AddOptions<ManagementAuthOptions>()
    .Bind(builder.Configuration.GetSection("ManagementAuth"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PublicUrlOptions>()
    .Bind(builder.Configuration)
    .Validate(
        options => PublicUrlOptions.IsValid(options.PublicBaseUrl),
        "PublicBaseUrl must be an absolute HTTP or HTTPS URL.")
    .ValidateOnStart();
builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
