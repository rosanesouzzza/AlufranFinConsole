#!/usr/bin/env dotnet-script
#r "nuget: Npgsql, 8.0.0"

using Npgsql;

Console.WriteLine("Setting up PostgreSQL database for Alufran Financial Console...\n");

var adminConnString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=;";
var dbName = "alufran_fin_close";
var dbUser = "alufran";
var dbPassword = "alufran";

using (var conn = new NpgsqlConnection(adminConnString))
{
    try
    {
        conn.Open();
        Console.WriteLine("✓ Connected to PostgreSQL");
    }
    catch (NpgsqlException e) when (e.Message.Contains("password"))
    {
        Console.WriteLine("! Empty password failed, trying with 'postgres' password...");
        adminConnString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;";
        conn.ConnectionString = adminConnString;
        conn.Open();
        Console.WriteLine("✓ Connected with default password");
    }

    using (var cmd = conn.CreateCommand())
    {
        try
        {
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\" ENCODING 'UTF8'";
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✓ Created database '{dbName}'");
    }

    using (var cmd = conn.CreateCommand())
    {
        try
        {
            cmd.CommandText = $"DROP USER IF EXISTS \"{dbUser}\"";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"CREATE USER \"{dbUser}\" WITH PASSWORD '{dbPassword}' CREATEDB";
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✓ Created user '{dbUser}'");
    }

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"GRANT ALL PRIVILEGES ON DATABASE \"{dbName}\" TO \"{dbUser}\"";
        cmd.ExecuteNonQuery();
        Console.WriteLine($"✓ Granted privileges");
    }
}

Console.WriteLine("\n✅ Database setup complete!");
Console.WriteLine("You can now run: dotnet ef database update");
