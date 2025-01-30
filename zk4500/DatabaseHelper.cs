using System;
using System.Data.SQLite;
using System.IO;

public class DatabaseHelper
{
    private static readonly string dbPath = "config.db";
    private static readonly string connectionString = $"Data Source={dbPath};Version=3;";

    public static void InitializeDatabase()
    {
        if (!File.Exists(dbPath))
        {
            SQLiteConnection.CreateFile(dbPath);
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string createTableQuery = "CREATE TABLE Config (Id INTEGER PRIMARY KEY AUTOINCREMENT, BaseUrl TEXT)";
                using (var cmd = new SQLiteCommand(createTableQuery, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }

    public static void SaveBaseUrl(string url)
    {
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string query = "INSERT OR REPLACE INTO Config (Id, BaseUrl) VALUES (1, @BaseUrl)";
            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@BaseUrl", url);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static string GetBaseUrl()
    {
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string query = "SELECT BaseUrl FROM Config WHERE Id = 1";
            using (var cmd = new SQLiteCommand(query, conn))
            {
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? "http://localhost/fingerprint-integration/api";
            }
        }
    }
}
