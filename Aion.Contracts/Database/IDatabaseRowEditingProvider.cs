namespace Aion.Contracts.Database;

/// <summary>
/// Marks a provider whose <see cref="IDatabaseProvider.Commands"/> generate grid edit statements that match
/// rows by primary key with correctly typed literals. Edit mode is only offered for these providers.
/// </summary>
public interface IDatabaseRowEditingProvider
{
}
