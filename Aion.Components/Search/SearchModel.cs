namespace Aion.Components.Search;

public class SearchModel
{
    public string Name { get; set; }
    
    public string Icon { get; set; }
    
    public ResultKind Kind { get; set; }

    public override string ToString() => Name;
}

public enum ResultKind
{
    Connection,
    Database,
    Table,
    Query
}