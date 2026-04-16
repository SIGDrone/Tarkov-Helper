using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string dbPath = @"c:\GitWork\Tarkov-Helper\TarkovDBEditor\tarkov_data.db";
        string connectionString = $"Data Source={dbPath}";

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // List all tables
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using (var reader = command.ExecuteReader())
                {
                    Console.WriteLine("Tables in the database:");
                    while (reader.Read())
                    {
                        Console.WriteLine("- " + reader.GetString(0));
                    }
                }
            }

            // Check if 'Maps' table exists and its content
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Maps';";
                var exists = command.ExecuteScalar();
                if (exists != null)
                {
                    Console.WriteLine("\nContent of 'Maps' table:");
                    command.CommandText = "SELECT * FROM Maps;";
                    using (var reader = command.ExecuteReader())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            Console.Write(reader.GetName(i) + "\t");
                        }
                        Console.WriteLine();
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.Write(reader.GetValue(i) + "\t");
                            }
                            Console.WriteLine();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n'Maps' table does not exist.");
                }
            }
        }
    }
}
