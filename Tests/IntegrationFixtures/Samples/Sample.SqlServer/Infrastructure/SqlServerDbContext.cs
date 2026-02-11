using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IntegrationFixtures.Sample.SqlServer.Infrastructure;

public sealed class SqlServerDbContext : DbContext
{
    private readonly IConfiguration _config;

    public SqlServerDbContext(IConfiguration config) => _config = config;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = _config.GetConnectionString("SqlServerDb");
        optionsBuilder.UseSqlServer(connectionString);
    }
}
