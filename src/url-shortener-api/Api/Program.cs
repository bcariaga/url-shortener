using UrlShortener.Application;
using UrlShortener.Infrastructure;
using UrlShortener.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOptions<ManagementAuthOptions>().Bind(builder.Configuration.GetSection("ManagementAuth")).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<PublicUrlOptions>().Bind(builder.Configuration).Validate(o => PublicUrlOptions.IsValid(o.PublicBaseUrl), "PublicBaseUrl must be an absolute HTTP or HTTPS URL.").ValidateOnStart();
builder.Services.AddAuthentication("Bearer").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TokenAuthHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
