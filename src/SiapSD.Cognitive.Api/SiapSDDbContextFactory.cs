using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SiapSD.Cognitive.Infrastructure;

public sealed class SiapSDDbContextFactory : IDesignTimeDbContextFactory<SiapSDDbContext>
{
    public SiapSDDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for design-time EF Core.");
        return new SiapSDDbContext(new DbContextOptionsBuilder<SiapSDDbContext>().UseNpgsql(connectionString).Options);
    }
}
