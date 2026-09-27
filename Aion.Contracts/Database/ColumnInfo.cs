namespace Aion.Contracts.Database;

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The column's type as the engine's catalog names it. SQLite keeps the declared text and MySQL reports its full
    /// COLUMN_TYPE, so both carry their own arguments ("decimal(10,2)", "tinyint(1)", "int unsigned"). PostgreSQL and
    /// SQL Server report only the name and give the length, precision and scale separately.
    /// </summary>
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

    /// <summary>
    /// Whether the engine computes the column from an expression over the row's other columns. Such a column
    /// takes no value at all, so inserts and updates leave it out. An identity column is not generated in this
    /// sense; see <see cref="IsIdentity"/>.
    /// </summary>
    public bool IsGenerated { get; set; }
    public int? MaxLength { get; set; }

    /// <summary>
    /// The precision and scale declared on an exact decimal column, for engines that report them beside
    /// <see cref="DataType"/>. Null when the column declares none, such as a bare PostgreSQL numeric.
    /// </summary>
    public int? NumericPrecision { get; set; }

    /// <inheritdoc cref="NumericPrecision"/>
    public int? NumericScale { get; set; }
    public ForeignKeyInfo? ForeignKey { get; set; }
    public bool IsForeignKey => ForeignKey != null;
}
