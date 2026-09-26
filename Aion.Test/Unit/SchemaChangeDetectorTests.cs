using Aion.Components.Connections;
using Shouldly;

namespace Aion.Test.Unit;

public class SchemaChangeDetectorTests
{
    [Theory]
    [InlineData("SELECT * FROM products", SchemaChange.None)]
    [InlineData("INSERT INTO products (name) VALUES ('create table')", SchemaChange.RowCounts)]
    [InlineData("UPDATE products SET name = 'drop' WHERE id = 1", SchemaChange.RowCounts)]
    [InlineData("  delete from products where id = 1", SchemaChange.RowCounts)]
    [InlineData("-- start over\nTRUNCATE products", SchemaChange.RowCounts)]
    [InlineData("REPLACE INTO products (id) VALUES (1)", SchemaChange.RowCounts)]
    [InlineData("MERGE INTO products USING incoming ON products.id = incoming.id WHEN MATCHED THEN DELETE", SchemaChange.RowCounts)]
    [InlineData("SELECT 1; DELETE FROM products", SchemaChange.RowCounts)]
    [InlineData("SELECT * FROM deleted_products", SchemaChange.None)]
    [InlineData("INSERT INTO products (id) VALUES (1); CREATE TABLE t (id int)", SchemaChange.Tables)]
    [InlineData("CREATE TABLE t (id int)", SchemaChange.Tables)]
    [InlineData("  create index ix on t (id)", SchemaChange.Tables)]
    [InlineData("ALTER TABLE t ADD COLUMN c int", SchemaChange.Tables)]
    [InlineData("DROP TABLE t", SchemaChange.Tables)]
    [InlineData("-- tidy up\nDROP VIEW v", SchemaChange.Tables)]
    [InlineData("/* setup */ CREATE TABLE t (id int)", SchemaChange.Tables)]
    [InlineData("SELECT 1; CREATE TABLE t (id int)", SchemaChange.Tables)]
    [InlineData("CREATE DATABASE shop", SchemaChange.Databases)]
    [InlineData("DROP DATABASE IF EXISTS shop", SchemaChange.Databases)]
    [InlineData("CREATE SCHEMA sales", SchemaChange.Databases)]
    [InlineData("CREATE TABLE t (id int); CREATE DATABASE other", SchemaChange.Databases)]
    public void Detect_ClassifiesWhatTheStatementsChange(string sql, SchemaChange expected)
    {
        SchemaChangeDetector.Detect(sql).ShouldBe(expected);
    }
}
