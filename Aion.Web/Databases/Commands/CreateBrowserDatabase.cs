using Aion.Contracts.Database;

namespace Aion.Web.Databases.Commands;

public record CreateBrowserDatabase(DatabaseType? Engine = null);
