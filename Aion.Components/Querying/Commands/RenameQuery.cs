namespace Aion.Components.Querying.Commands;

public record RenameQuery(QueryModel Query, string Name);

public record RenameActiveQuery(string Name);

public record PromptRenameQuery(QueryModel Query);

public record PromptRenameActiveQuery();