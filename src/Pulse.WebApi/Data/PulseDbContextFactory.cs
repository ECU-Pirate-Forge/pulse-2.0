using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pulse.WebApi.Data;

public class PulseDbContextFactory : IDesignTimeDbContextFactory<PulseDbContext>
{
    public PulseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PulseDbContext>();
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=PulseDb;Trusted_Connection=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);

        return new PulseDbContext(optionsBuilder.Options);
    }
}
