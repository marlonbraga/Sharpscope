using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IntegrationFixtures.Sample.Postgres.Infrastructure;

public sealed class PostgresDbContext : DbContext
{
    private readonly IConfiguration _config;

    public PostgresDbContext(IConfiguration config) => _config = config;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = _config.GetConnectionString("PostgresDb");
        optionsBuilder.UseNpgsql(connectionString);
    }
}
