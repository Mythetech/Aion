namespace Aion.Contracts.Database;

/// <summary>
/// Marks a server provider whose connection can create further databases with the CREATE DATABASE statement
/// from <see cref="IStandardDatabaseCommands.GenerateCreateDatabaseScript"/>. In-browser and file based engines
/// hold one database per connection, so Add Database is only offered for these providers.
/// </summary>
public interface IDatabaseCreationProvider
{
}
