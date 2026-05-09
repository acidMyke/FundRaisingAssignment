using FundRaisingAssignment.Application.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Test;

/// <summary>
/// Owns a SQLite in-memory connection + ApplicationDbContext for a single test.
/// Disposing closes the connection, which drops the in-memory database.
/// </summary>
internal sealed class TestDb : IDisposable
{
    public SqliteConnection Connection { get; }
    public ApplicationDbContext Context { get; }

    public TestDb()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(Connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
