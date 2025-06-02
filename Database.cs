using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Samples.Azure.Database.NL2SQL;

public class TableInfo
{
    public string? table_name;
    public string? table_description;
}

public class ColumnInfo
{
    public string? column_name;
    public string? column_type;
    public string? column_description;
}

public class Database(string connectionString)
{
    private readonly string connectionString = connectionString;

    public string GetConnectionString()
    {
        return connectionString;
    }
    
    public async Task<string> GetTableColumnsAsync(string tableName)
    {
        await using var connection = new SqlConnection(connectionString);
        var columns = await connection.QueryAsync<ColumnInfo>("""
            select
                quotename(c.name) as column_name,
                t.name as column_type,
                cast(ep.[value] as nvarchar(1000)) as [column_description]
            from
                sys.tables s
            left join
                sys.extended_properties ep on ep.major_id = s.object_id
            inner join
                sys.columns c on ep.minor_id = c.column_id and ep.major_id = c.object_id
            inner join
                sys.types t on c.system_type_id = t.system_type_id and c.user_type_id = t.user_type_id
            where
                s.type_desc = 'USER_TABLE'
            and
                ep.minor_id > 0
            and
                ep.major_id = object_id(@TableName)
            and
                ep.class_desc = 'OBJECT_OR_COLUMN'
            and
                ep.[name] = 'MS_Description'
            order by
                c.column_id
        """,
        new { TableName = tableName });

        var schema = new StringBuilder();
        foreach (var column in columns)
        {
            schema.AppendLine($"{column.column_name} ({column.column_type}) -- {column.column_description}");
        }
        // Console.WriteLine($"Schema for table '{tableName}':");
        // Console.WriteLine(schema.ToString());

        return schema.ToString();
    }

    public async Task<string> GetTablesAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        var tables = await connection.QueryAsync<TableInfo>("""
            select
                quotename(schema_name([schema_id])) + '.' + quotename(s.[name]) as table_name,
                ep.[value] as table_description
            from
                sys.tables s
            left join
                sys.extended_properties ep on ep.major_id = s.object_id
            where
                s.type_desc = 'USER_TABLE'
            and
                ep.minor_id = 0
        """
        );

        var schema = new StringBuilder();
        foreach (var table in tables)
        {
            schema.AppendLine($"{table.table_name}: {table.table_description}");
        }

        return schema.ToString();
    }
}