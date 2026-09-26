namespace Aion.Contracts.Database;

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// The engine's own name for the type, where <see cref="DataType"/> only names its category. PostgreSQL
    /// reports arrays as ARRAY and enums, composites and extension types as USER-DEFINED, and keeps the real
    /// name (_int4, mood) in udt_name. Null for engines that name the type directly.
    /// </summary>
    public string? UdtName { get; set; }
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public int? MaxLength { get; set; }
    public ForeignKeyInfo? ForeignKey { get; set; }
    public bool IsForeignKey => ForeignKey != null;
}
