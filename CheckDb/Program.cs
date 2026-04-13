using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0) return;
        string dbPath = args[0];
        Console.WriteLine($"Checking DB: {dbPath}");

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Name, MarkerType, MapKey, FloorId FROM MapMarkers WHERE MarkerType LIKE '%Extraction%'";
            Console.WriteLine("Extractions in DB:");
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    string type = reader.GetString(1);
                    string mapKey = reader.GetString(2);
                    string floorId = reader.IsDBNull(3) ? "<NULL>" : reader.GetString(3);
                    Console.WriteLine($" - {name} ({type}) Map: {mapKey}, Floor: {floorId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
