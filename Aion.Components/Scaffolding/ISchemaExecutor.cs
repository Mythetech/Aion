using Aion.Contracts.Connections;

namespace Aion.Components.Scaffolding;

/// <summary>
/// Creates the database and tables described by the schema wizard and connects to it.
/// </summary>
public interface ISchemaExecutor
{
    /// <summary>
    /// Creates every table or none of them. A failure is reported in the result, with the engine's own message,
    /// rather than thrown, so the wizard can stay open for the user to correct the definition.
    /// </summary>
    Task<SchemaExecutionResult> ExecuteAsync(SchemaWizardModel model);
}

public sealed record SchemaExecutionResult(ConnectionModel? Connection, string? Error)
{
    public bool Success => Error is null;

    public static SchemaExecutionResult Created(ConnectionModel connection) => new(connection, null);

    public static SchemaExecutionResult Failed(string error) => new(null, error);
}
