using LeaveTracker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeaveTracker.Tests;

/// <summary>
/// Hosts the API in memory against a SQLite in-memory database.
/// Pass an existing open connection to share one database between several hosts (restart tests).
/// </summary>
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    public TestAppFactory()
        : this(new SqliteConnection("Data Source=:memory:"), ownsConnection: true)
    {
    }

    public TestAppFactory(SqliteConnection sharedConnection)
        : this(sharedConnection, ownsConnection: false)
    {
    }

    private TestAppFactory(SqliteConnection connection, bool ownsConnection)
    {
        _connection = connection;
        _ownsConnection = ownsConnection;
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LeaveDbContext>>();
            services.RemoveAll<LeaveDbContext>();
            services.AddDbContext<LeaveDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public LeaveDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LeaveDbContext>().UseSqlite(_connection).Options);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsConnection)
        {
            _connection.Dispose();
        }
    }
}
