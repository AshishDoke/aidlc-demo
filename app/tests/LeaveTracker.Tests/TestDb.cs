using LeaveTracker.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

/// <summary>
/// A fresh SQLite in-memory database for one test. The connection stays open for the
/// lifetime of this object, so the database lives as long as the test needs it.
/// </summary>
public sealed class TestDb : IDisposable
{
    public SqliteConnection Connection { get; }

    public TestDb()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public LeaveDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlite(Connection).Options);

    public void Dispose() => Connection.Dispose();
}
