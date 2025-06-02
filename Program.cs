using System.CommandLine;
using Azure.Identity;
using Samples.Azure.Database.NL2SQL;

var rootCommand = new RootCommand();

var envFileOption = new Option<string>(
    name: "--env-file", 
    description: "The .env file to load environment variables from.",
    getDefaultValue: () => ".env");
envFileOption.AddAlias("-e"); 

var debugOption = new Option<Boolean>(
    name: "--debug", 
    description: "Enable debug mode.",
    getDefaultValue: () => false);

var chatCommand = new Command("chat", "Run the chatbot");
chatCommand.AddOption(envFileOption); 
chatCommand.AddOption(debugOption); 
chatCommand.SetHandler(async (envFileOptionValue, debugOptionValue) => 
    {
        var chatBot = new ChatBot(envFileOptionValue);
        await chatBot.RunAsync(debugOptionValue);
    },
    envFileOption, debugOption);
rootCommand.Add(chatCommand);

await rootCommand.InvokeAsync(args);
