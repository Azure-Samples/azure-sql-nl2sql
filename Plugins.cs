using System.ComponentModel;
using Microsoft.SemanticKernel;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using DotNetEnv;
using System.Text;
using Microsoft.Identity.Client;
using System.Runtime.CompilerServices;

#pragma warning disable SKEXP0001

namespace Samples.Azure.Database.NL2SQL;

public class DatabaseQueryPlugin(Kernel kernel, ILogger logger, Database database)
{
    private readonly ILogger logger = logger;
    private readonly Kernel kernel = kernel;
    private readonly Database database = database;

    [KernelFunction("QueryDatabase")]
    [Description("""
        Run a query against the database using the provided list of tables. 
        The tables must be provided in the format: "table1, table2, table3".
        The database being used is Microsoft SQL Server so you must use T-SQL syntax.
        """)]
    public async Task<IEnumerable<dynamic>> QueryDatabase(string list_of_tables, string explanation_of_what_to_return)
    {
        logger.LogInformation($"Querying the database using '{list_of_tables}'");
        logger.LogInformation($"Request is: '{explanation_of_what_to_return}'");

        var table_schemas = new StringBuilder();
        foreach (var table in list_of_tables.Split(',').Select(t => t.Trim()))
        {
            if (string.IsNullOrWhiteSpace(table))
            {
                logger.LogWarning("Empty table name provided, skipping.");
                continue;
            }

            var tableSchema = await database.GetTableColumnsAsync(table);
            if (string.IsNullOrWhiteSpace(tableSchema))
            {
                logger.LogWarning($"No schema found for table '{table}', skipping.");
                continue;
            }

            logger.LogInformation($"Adding schema for table '{table}'...");
            table_schemas.AppendLine($"{table}:");
            table_schemas.AppendLine(tableSchema);
            table_schemas.AppendLine();
        }

        var ai = kernel.GetRequiredService<IChatCompletionService>();
        var chat = new ChatHistory($"""
        You create T-SQL queries based on the given user request and the provided schema. Just return T-SQL query to be executed. 
        Do not return other text or explanation. Don't use markdown or any wrappers.
        The schema is provided in the format: 
        
        Table1Name: 
        Column1Name (Column1Type) -- Column1Description
        Column2Name (Column2Type) -- Column2Description
        ...
        ColumnNName (ColumnNType) -- ColumnNDescription

        Table2Name: 
        Column1Name (Column1Type) -- Column1Description
        Column2Name (Column2Type) -- Column2Description
        ...
        ColumnNName (ColumnNType) -- ColumnNDescription

        The schema for the avaiable tables is the following:
        
        {table_schemas.ToString()}
        
        Generate the T-SQL query based on the provided schema and the user request. The user request is in the next message.
        """);

        chat.AddUserMessage(explanation_of_what_to_return);
        var response = await ai.GetChatMessageContentAsync(chat);
        if (response.Content == null)
        {
            logger.LogWarning("AI was not able to generate a SQL query.");
            return [];
        }

        string sqlQuery = response.Content.Replace("```sql", "").Replace("```", "");

        logger.LogInformation($"Executing the following query: {sqlQuery}");

        await using var connection = new SqlConnection(database.GetConnectionString());
        var result = await connection.QueryAsync(sqlQuery);

        return result;
    }   
}