using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Data;

namespace UpgradePlanner.Tests;

/// <summary>
/// A throwaway database for one test.
/// </summary>
/// <remarks>
/// <para>
/// This uses <b>real SQLite in memory</b>, not the EF in-memory provider. That
/// matters: the in-memory provider silently ignores check constraints and
/// foreign keys, so a test suite built on it would pass while
/// <c>CK_Upgrade_Priority</c> and <c>DeleteBehavior.Restrict</c> were quietly
/// broken — exactly the guarantees worth testing.
/// </para>
/// <para>
/// Schema is created by running the migrations, so every test also re-proves
/// that migrations apply cleanly from an empty database.
/// </para>
/// </remarks>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }

    public TestDatabase()
    {
        // The database lives as long as the connection does, so it is held open
        // for the lifetime of the fixture and dropped with it.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);

        Db.Database.Migrate();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
