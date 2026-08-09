using Tradebook.Infrastructure.Migrations;

var connectionString = args.Length switch
{
    1 => args[0],
    _ => Environment.GetEnvironmentVariable("TRADEBOOK_DATABASE_CONNECTION_STRING")
};

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: Tradebook.Migrations <connection-string> (or set TRADEBOOK_DATABASE_CONNECTION_STRING).");
    return 2;
}

MigrationRunner.Run(connectionString);
return 0;
