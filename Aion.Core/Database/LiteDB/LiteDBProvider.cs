using Aion.Core.Queries;
using LiteDB;

namespace Aion.Core.Database.LiteDB;

public class LiteDBProvider : IDatabaseProvider
{
    private readonly Dictionary<string, (LiteDatabase Db, bool InTransaction)> _activeTransactions = new();

    public IStandardDatabaseCommands Commands { get; } = new LiteDBCommands();
    public DatabaseType DatabaseType => DatabaseType.LiteDB;

    public Task<List<string>?> GetDatabasesAsync(string connectionString)
    {
        var cs = new ConnectionString(connectionString);
        var filename = Path.GetFileNameWithoutExtension(cs.Filename) ?? "Database";
        return Task.FromResult<List<string>?>(new List<string> { filename });
    }

    public Task<List<string>> GetTablesAsync(string connectionString, string database)
    {
        var collections = new List<string>();

        try
        {
            using var db = new LiteDatabase(connectionString);

            foreach (var name in db.GetCollectionNames())
            {
                if (!name.StartsWith('$'))
                {
                    collections.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LiteDB GetTablesAsync error: {ex.Message}");
            throw;
        }

        return Task.FromResult(collections.OrderBy(c => c).ToList());
    }

    public Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string database, string collection)
    {
        var columns = new Dictionary<string, ColumnInfo>();

        using var db = new LiteDatabase(connectionString);
        var col = db.GetCollection(collection);

        const int sampleSize = 100;
        var documents = col.FindAll().Take(sampleSize).ToList();

        columns["_id"] = new ColumnInfo
        {
            Name = "_id",
            DataType = "ObjectId",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = true
        };

        foreach (var doc in documents)
        {
            InferSchemaFromDocument(doc, columns, "");
        }

        return Task.FromResult(columns.Values
            .OrderBy(c => c.Name == "_id" ? "" : c.Name)
            .ToList());
    }

    private void InferSchemaFromDocument(BsonDocument doc, Dictionary<string, ColumnInfo> columns, string prefix)
    {
        foreach (var element in doc)
        {
            var fieldName = string.IsNullOrEmpty(prefix) ? element.Key : $"{prefix}.{element.Key}";

            if (fieldName == "_id") continue; // Already handled

            var bsonType = GetBsonTypeName(element.Value);

            if (!columns.ContainsKey(fieldName))
            {
                columns[fieldName] = new ColumnInfo
                {
                    Name = fieldName,
                    DataType = bsonType,
                    IsNullable = true, // All fields nullable in schemaless DB
                    IsPrimaryKey = false,
                    IsIdentity = false
                };
            }
            else
            {
                if (columns[fieldName].DataType != bsonType &&
                    columns[fieldName].DataType != "Mixed" &&
                    bsonType != "Null")
                {
                    columns[fieldName] = new ColumnInfo
                    {
                        Name = fieldName,
                        DataType = "Mixed",
                        IsNullable = true,
                        IsPrimaryKey = false,
                        IsIdentity = false
                    };
                }
            }

            if (element.Value.IsDocument)
            {
                InferSchemaFromDocument(element.Value.AsDocument, columns, fieldName);
            }
        }
    }

    private static string GetBsonTypeName(BsonValue value)
    {
        return value.Type switch
        {
            BsonType.Null => "Null",
            BsonType.Int32 => "Int32",
            BsonType.Int64 => "Int64",
            BsonType.Double => "Double",
            BsonType.Decimal => "Decimal",
            BsonType.String => "String",
            BsonType.Document => "Document",
            BsonType.Array => "Array",
            BsonType.Binary => "Binary",
            BsonType.ObjectId => "ObjectId",
            BsonType.Guid => "Guid",
            BsonType.Boolean => "Boolean",
            BsonType.DateTime => "DateTime",
            BsonType.MinValue => "MinValue",
            BsonType.MaxValue => "MaxValue",
            _ => "Unknown"
        };
    }

    public Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken)
    {
        var result = new QueryResult();

        try
        {
            using var db = new LiteDatabase(connectionString);

            var reader = db.Execute(query);

            if (reader.HasValues)
            {
                var isFirstRow = true;

                foreach (var value in reader.ToEnumerable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (value.IsDocument)
                    {
                        var doc = value.AsDocument;

                        // Build columns from first document
                        if (isFirstRow)
                        {
                            foreach (var key in doc.Keys)
                            {
                                result.Columns.Add(key);
                            }
                            isFirstRow = false;
                        }

                        var row = new Dictionary<string, object>();
                        foreach (var col in result.Columns)
                        {
                            if (doc.ContainsKey(col))
                            {
                                row[col] = ConvertBsonValue(doc[col])!;
                            }
                            else
                            {
                                row[col] = null!;
                            }
                        }

                        foreach (var key in doc.Keys)
                        {
                            if (!result.Columns.Contains(key))
                            {
                                result.Columns.Add(key);
                                row[key] = ConvertBsonValue(doc[key])!;
                            }
                        }

                        result.Rows.Add(row);
                    }
                    else
                    {
                        if (isFirstRow)
                        {
                            result.Columns.Add("Result");
                            isFirstRow = false;
                        }
                        result.Rows.Add(new Dictionary<string, object>
                        {
                            ["Result"] = ConvertBsonValue(value)!
                        });
                    }
                }
            }

            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return Task.FromResult(result);
        }
    }

    private static object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsNull) return null;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsDecimal) return value.AsDecimal;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsDateTime) return value.AsDateTime;
        if (value.IsGuid) return value.AsGuid;
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsDocument) return JsonSerializer.Serialize(value.AsDocument);
        if (value.IsArray) return JsonSerializer.Serialize(value.AsArray);
        if (value.IsBinary) return Convert.ToBase64String(value.AsBinary);

        return value.ToString();
    }

    public string UpdateConnectionString(string connectionString, string database)
    {
        return connectionString;
    }

    public int GetDefaultPort() => -1; // File-based, no port

    public bool ValidateConnectionString(string connectionString, out string? error)
    {
        try
        {
            var cs = new ConnectionString(connectionString);

            if (string.IsNullOrEmpty(cs.Filename))
            {
                error = "Filename is required";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public Task<QueryPlan> GetEstimatedPlanAsync(string connectionString, string query)
    {
        return Task.FromResult(new QueryPlan
        {
            PlanType = "Estimated",
            PlanFormat = "TEXT",
            PlanContent = "Query plans are not supported for LiteDB.\n\nLiteDB uses internal indexing for query optimization.\nUse EXPLAIN pragma commands to view index usage:\n\nSELECT $ FROM $indexes"
        });
    }

    public Task<QueryPlan> GetActualPlanAsync(string connectionString, string query)
    {
        return Task.FromResult(new QueryPlan
        {
            PlanType = "Actual",
            PlanFormat = "TEXT",
            PlanContent = "Query plans are not supported for LiteDB.\n\nLiteDB uses internal indexing for query optimization.\nUse EXPLAIN pragma commands to view index usage:\n\nSELECT $ FROM $indexes"
        });
    }

    public Task<TransactionInfo> BeginTransactionAsync(string connectionString)
    {
        var transactionInfo = new TransactionInfo();

        var db = new LiteDatabase(connectionString);
        db.BeginTrans();

        _activeTransactions[transactionInfo.Id] = (db, true);

        return Task.FromResult(transactionInfo);
    }

    public Task CommitTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var entry))
        {
            entry.Db.Commit();
            entry.Db.Dispose();
            _activeTransactions.Remove(transactionId);
        }
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(string connectionString, string transactionId)
    {
        if (_activeTransactions.TryGetValue(transactionId, out var entry))
        {
            entry.Db.Rollback();
            entry.Db.Dispose();
            _activeTransactions.Remove(transactionId);
        }
        return Task.CompletedTask;
    }

    public Task<QueryResult> ExecuteInTransactionAsync(string connectionString, string query, string transactionId, CancellationToken cancellationToken)
    {
        if (!_activeTransactions.TryGetValue(transactionId, out var entry))
        {
            return Task.FromResult(new QueryResult { Error = "Transaction not found" });
        }

        var result = new QueryResult();
        try
        {
            var reader = entry.Db.Execute(query);

            if (reader.HasValues)
            {
                var isFirstRow = true;

                foreach (var value in reader.ToEnumerable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (value.IsDocument)
                    {
                        var doc = value.AsDocument;

                        if (isFirstRow)
                        {
                            foreach (var key in doc.Keys)
                            {
                                result.Columns.Add(key);
                            }
                            isFirstRow = false;
                        }

                        var row = new Dictionary<string, object>();
                        foreach (var col in result.Columns)
                        {
                            row[col] = doc.ContainsKey(col) ? ConvertBsonValue(doc[col])! : null!;
                        }
                        result.Rows.Add(row);
                    }
                    else
                    {
                        if (isFirstRow)
                        {
                            result.Columns.Add("Result");
                            isFirstRow = false;
                        }
                        result.Rows.Add(new Dictionary<string, object>
                        {
                            ["Result"] = ConvertBsonValue(value)!
                        });
                    }
                }
            }

            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return Task.FromResult(result);
        }
    }
}
