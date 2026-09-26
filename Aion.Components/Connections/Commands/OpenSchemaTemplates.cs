namespace Aion.Components.Connections.Commands;

/// <summary>
/// Opens a new tab with a CREATE TABLE statement for the connection's engine. Nothing runs until the user
/// edits it and presses Run.
/// </summary>
public record OpenCreateTableTemplate(Guid ConnectionId, string DatabaseName);

/// <summary>
/// Opens a new tab with a CREATE DATABASE statement for the connection's server. Nothing runs until the user
/// edits it and presses Run.
/// </summary>
public record OpenCreateDatabaseTemplate(Guid ConnectionId);
