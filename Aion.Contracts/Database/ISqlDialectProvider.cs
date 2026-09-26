using Aion.Contracts.Database.Dialects;

namespace Aion.Contracts.Database;

/// <summary>
/// A provider whose engine runs SQL, exposing the dialect its own statements are quoted with so SQL built
/// elsewhere in Aion, such as foreign key lookups, follows the same identifier and literal rules.
/// </summary>
public interface ISqlDialectProvider
{
    SqlDialect Dialect { get; }
}
