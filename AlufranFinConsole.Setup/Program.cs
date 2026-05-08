using Npgsql;

Console.WriteLine("🚀 Alufran Financial Console - Database Setup\n");

string adminConnString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;";
string dbName = "alufran_fin_close";
string dbUser = "alufran";
string dbPassword = "alufran";

// Try different authentication methods
List<string> passwords = new() { "", "postgres", "password", "admin" };
NpgsqlConnection? conn = null;

foreach (var pwd in passwords)
{
    try
    {
        var connBuilder = new NpgsqlConnectionStringBuilder(adminConnString)
        {
            Password = pwd,
            SslMode = SslMode.Disable
        };

        conn = new NpgsqlConnection(connBuilder.ConnectionString);
        conn.Open();
        Console.WriteLine($"✓ Connected to PostgreSQL (password: {(string.IsNullOrEmpty(pwd) ? "none" : pwd)})");
        break;
    }
    catch (NpgsqlException)
    {
        if (conn != null)
        {
            try { conn.Close(); } catch { }
            conn.Dispose();
        }
        conn = null;
        continue;
    }
}

if (conn == null)
{
    Console.WriteLine("✗ Failed to connect to PostgreSQL with any password");
    Console.WriteLine("Please ensure PostgreSQL is running and accessible at localhost:5432");
    Environment.Exit(1);
}

try
{
    conn.ChangeDatabase("postgres");

    // Drop existing database if exists
    try
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)";
            cmd.ExecuteNonQuery();
        }
    }
    catch { }

    // Create database
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\" ENCODING 'UTF8'";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine($"✓ Created database '{dbName}'");

    // Drop user if exists
    try
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DROP USER IF EXISTS \"{dbUser}\"";
            cmd.ExecuteNonQuery();
        }
    }
    catch { }

    // Create user
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"CREATE USER \"{dbUser}\" WITH PASSWORD '{dbPassword}' CREATEDB";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine($"✓ Created user '{dbUser}'");

    // Grant privileges
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"GRANT ALL PRIVILEGES ON DATABASE \"{dbName}\" TO \"{dbUser}\"";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine($"✓ Granted privileges");

    Console.WriteLine("\n✅ Database setup complete!");
    Console.WriteLine("\nNext steps:");
    Console.WriteLine("  cd AlufranFinConsole.Web");
    Console.WriteLine("  dotnet ef database update");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
    Environment.Exit(1);
}
finally
{
    conn?.Close();
    conn?.Dispose();
}
