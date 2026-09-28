using FindIt.Api.Data;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
var configuredTenantId = builder.Configuration["AzureAd:TenantId"];
var configuredApiClientId = builder.Configuration["AzureAd:ClientId"];
var tenantIsConfigured = Guid.TryParse(configuredTenantId, out var allowedTenantId)
    && allowedTenantId != Guid.Empty;
var apiIsConfigured = Guid.TryParse(configuredApiClientId, out var apiClientId)
    && apiClientId != Guid.Empty;
var webIsConfigured = Guid.TryParse(builder.Configuration["FindItAuth:WebClientId"],
    out var allowedWebClientId) && allowedWebClientId != Guid.Empty;
var personalTestMode = builder.Configuration.GetValue<bool>("FindItAuth:PersonalTestMode");
var consumerTenantId = Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad");
if (personalTestMode && (!builder.Environment.IsDevelopment()
    || !tenantIsConfigured || allowedTenantId != consumerTenantId))
{
    throw new InvalidOperationException(
        "O teste com contas pessoais exige Development e o locatário Microsoft consumers.");
}
builder.Services.AddSwaggerGen(options =>
{
    if (!tenantIsConfigured || !apiIsConfigured || !webIsConfigured)
    {
        return;
    }

    // O primeiro consentimento de uma API multidiretório usa as permissões
    // delegadas já declaradas no registro FindIt Web.
    var consentScope = $"api://{apiClientId}/.default";
    var authority = $"https://login.microsoftonline.com/{allowedTenantId}/oauth2/v2.0/";
    options.AddSecurityDefinition("FindIt Web", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(authority + "authorize?prompt=consent"),
                TokenUrl = new Uri(authority + "token"),
                Scopes = new Dictionary<string, string>
                {
                    [consentScope] = "Consentir o acesso do FindIt Web à API FindIt"
                }
            }
        }
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("FindIt Web", document)] = [consentScope]
    });
});
if (tenantIsConfigured && apiIsConfigured && webIsConfigured)
{
    authentication.AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd");
}
else
{
    // Consultas públicas funcionam mesmo se a configuração de identidade estiver incompleta;
    // nenhuma rota autenticada aceita tokens nesse caso.
    authentication.AddJwtBearer();
}
builder.Services.AddAuthorization(options =>
{
    var institutionalPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireAssertion(context => tenantIsConfigured && apiIsConfigured && webIsConfigured
            && InstitutionalTokenValidator.IsAllowed(context.User, allowedTenantId, allowedWebClientId))
        .Build();
    options.DefaultPolicy = institutionalPolicy;
    options.FallbackPolicy = institutionalPolicy;
});
builder.Services.AddScoped<ItemQueryService>();
builder.Services.AddScoped<RequestExpirationService>();
builder.Services.AddHostedService<RequestExpirationWorker>();
builder.Services.AddScoped<RecoveryWorkflowService>();
builder.Services.AddScoped<ItemManagementService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AdministrationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<FindItDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("FindIt");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Configure ConnectionStrings:FindIt usando Secret Manager ou variável de ambiente.");
    }

    options.UseMySQL(connectionString);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        if (webIsConfigured && apiIsConfigured)
        {
            options.OAuthClientId(allowedWebClientId.ToString());
            options.OAuthAppName("FindIt Web");
            options.OAuthScopes($"api://{apiClientId}/.default");
            options.OAuthUsePkce();
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
