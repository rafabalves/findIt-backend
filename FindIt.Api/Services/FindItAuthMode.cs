namespace FindIt.Api.Services;

public static class FindItAuthMode
{
    public static bool AllowsAnyMicrosoftAccountForTesting(
        IConfiguration configuration, IHostEnvironment environment) =>
        (environment.IsDevelopment() || environment.IsEnvironment("Demo"))
        && configuration.GetValue<bool>("FindItAuth:AllowAnyMicrosoftAccountForTesting")
        && string.Equals(configuration["AzureAd:TenantId"], "common",
            StringComparison.OrdinalIgnoreCase);
}
