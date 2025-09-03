using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.SqlServer;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.Logging.Console;
using DotNetEnv;
using System.Text.Json;
using Spectre.Console;
using Microsoft.SemanticKernel.Services;
using Azure.Identity;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Data.SqlClient;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020

namespace Samples.Azure.Database.NL2SQL;

public class ChatBot
{
    private readonly string azureOpenAIEndpoint;
    private readonly string azureOpenAIApiKey;
    private readonly string chatModelDeploymentName;
    private readonly string sqlConnectionString;

    public ChatBot(string envFile)
    {
        Env.Load(envFile);
        azureOpenAIEndpoint = Env.GetString("OPENAI_URL");
        azureOpenAIApiKey = Env.GetString("OPENAI_KEY") ?? string.Empty;
        chatModelDeploymentName = Env.GetString("OPENAI_CHAT_DEPLOYMENT_NAME");
        sqlConnectionString = Env.GetString("MSSQL_CONNECTION_STRING");
    }

    public async Task RunAsync(bool enableDebug = false)
    {
        AnsiConsole.Clear();
        AnsiConsole.Foreground = Color.Green;

        var table = new Table();
        table.Expand();
        table.AddColumn(new TableColumn("[bold]Natural Language Database Chatbot Agent[/] v1.1").Centered());
        AnsiConsole.Write(table);

        var database = new Database(sqlConnectionString);

        var openAIPromptExecutionSettings = new AzureOpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        (var logger, var kernel, var ai) = AnsiConsole.Status().Start("Booting up agent...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Default);
            ctx.SpinnerStyle(Style.Parse("yellow"));

            AnsiConsole.WriteLine("Initializing kernel...");
            var credentials = new DefaultAzureCredential();
            var sc = new ServiceCollection();

            if (string.IsNullOrEmpty(azureOpenAIApiKey))
            {
                sc.AddAzureOpenAIChatCompletion(chatModelDeploymentName, azureOpenAIEndpoint, credentials);
            }
            else
            {
                sc.AddAzureOpenAIChatCompletion(chatModelDeploymentName, azureOpenAIEndpoint, azureOpenAIApiKey);
            }

            sc.AddKernel();
            sc.AddLogging(b => 
            {
                b.ClearProviders(); // Clear default providers
                b.SetMinimumLevel(enableDebug ? LogLevel.Debug : LogLevel.None);
                b.AddProvider(new SpectreConsoleLoggerProvider());
            });

            var services = sc.BuildServiceProvider();
            var logger = services.GetRequiredService<ILogger<Program>>();

            AnsiConsole.WriteLine("Initializing plugins...");
            var kernel = services.GetRequiredService<Kernel>();            
            kernel.Plugins.AddFromObject(new DatabaseQueryPlugin(kernel, logger, database));

            foreach (var p in kernel.Plugins)
            {
                foreach (var f in p.GetFunctionsMetadata())
                {
                    AnsiConsole.WriteLine($"Plugin: {p.Name}, Function: {f.Name}");
                }
            }
            var ai = kernel.GetRequiredService<IChatCompletionService>();

            AnsiConsole.WriteLine("Initializing database...");
            var b = new SqlConnectionStringBuilder(sqlConnectionString);
            AnsiConsole.WriteLine($"Server: {b.DataSource}, Database: {b.InitialCatalog}");
            database.Initialize();

            AnsiConsole.WriteLine("Done!");

            return (logger, kernel, ai);
        });

        AnsiConsole.WriteLine("Ready to chat! Hit 'ctrl-c' to quit.");
        var chat = new ChatHistory($"""
            You are an AI assistant that helps users to query the database. The tables in the database, with the related description, are:

            {await database.GetTablesAsync()}                    

            Use a professional tone when aswering and provide a summary of data instead of lists. 
            If users ask about topics you don't know, answer that you don't know. Today's date is {DateTime.Now:yyyy-MM-dd}. 
            You must answer providing a list of tables that must be used to answer the question and an explanation of that you'll be doing to answer the question.
            You must use the provided tool to query the database.
            If the request is complex, break it down into smaller steps and call the plugin as many time as needed. Ideally don't use most than 5 tables in the same query.            
        """);
        var builder = new StringBuilder();
        while (true)
        {
            AnsiConsole.WriteLine();
            var question = AnsiConsole.Prompt(new TextPrompt<string>($"🧑: "));

            if (string.IsNullOrWhiteSpace(question))
                continue;

            switch (question)
            {
                case "/c":
                    AnsiConsole.Clear();
                    continue;
                case "/ch":
                    chat.RemoveRange(1, chat.Count - 1);
                    AnsiConsole.WriteLine("Chat history cleared.");
                    continue;
                case "/h":
                    foreach (var message in chat)
                    {
                        AnsiConsole.WriteLine($"> ---------- {message.Role} ----------");
                        AnsiConsole.WriteLine($"> MESSAGE  > {message.Content}");
                        AnsiConsole.WriteLine($"> METADATA > {JsonSerializer.Serialize(message.Metadata)}");
                        AnsiConsole.WriteLine($"> ------------------------------------");
                    }
                    continue;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("🤖: Formulating answer...");
            builder.Clear();
            chat.AddUserMessage(question);
            var firstLine = true;
            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chat, openAIPromptExecutionSettings, kernel))
            {
                if (!enableDebug)
                    if (firstLine && message.Content != null && message.Content.Length > 0)
                    {
                        AnsiConsole.Cursor.MoveUp();
                        AnsiConsole.WriteLine("                                  ");
                        AnsiConsole.Cursor.MoveUp();
                        AnsiConsole.Write($"🤖: ");
                        firstLine = false;
                    }
                AnsiConsole.Write(message.Content ?? string.Empty);
                builder.Append(message.Content);
            }
            AnsiConsole.WriteLine();

            chat.AddAssistantMessage(builder.ToString());
        }
    }

   
}

