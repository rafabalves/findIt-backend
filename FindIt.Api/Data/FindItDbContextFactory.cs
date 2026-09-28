using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FindIt.Api.Data;

public sealed class FindItDbContextFactory : IDesignTimeDbContextFactory<FindItDbContext>
{
    public FindItDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // A conexão fictícia permite gerar migrations sem um banco configurado.
        // Quando há segredo local ou variável de ambiente, o EF usa a conexão real.
        const string designTimeConnection =
            "server=localhost;port=3306;database=findit;user=design_time;password=design_time;SslMode=Disabled;AllowPublicKeyRetrieval=True";
        var connectionString = configuration.GetConnectionString("FindIt");
        var options = new DbContextOptionsBuilder<FindItDbContext>()
            .UseMySQL(string.IsNullOrWhiteSpace(connectionString)
                ? designTimeConnection : connectionString)
            .Options;
        return new FindItDbContext(options);
    }
}
