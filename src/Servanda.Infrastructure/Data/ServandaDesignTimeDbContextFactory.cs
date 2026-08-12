using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Servanda.Infrastructure.Data;

public sealed class ServandaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ServandaDbContext>
{
    public ServandaDbContext CreateDbContext(string[] args)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(Path.GetTempPath(), "servanda-design.db"),
            ForeignKeys = true,
        }.ToString();
        var options = new DbContextOptionsBuilder<ServandaDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new ServandaDbContext(options);
    }
}
