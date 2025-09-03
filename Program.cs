using System.CommandLine;
using Azure.Identity;
using Samples.Azure.Database.NL2SQL;

var rootCommand = new RootCommand();

var envFileOption = new Option<string>("--env-file")
{
    DefaultValueFactory = _ => ".env"
};
envFileOption.Aliases.Add("-e");
envFileOption.Description = "The .env file to load environment variables from.";


var debugOption = new Option<Boolean>("--debug")
{
    DefaultValueFactory = _ => false,
    Description = "Enable debug mode."
};

var chatCommand = new Command("chat", "Run the chatbot");
chatCommand.Options.Add(envFileOption);
chatCommand.Options.Add(debugOption);
chatCommand.SetAction(async parseResult =>
{
    var envFileOptionValue = parseResult.GetValue(envFileOption);
    var debugOptionValue = parseResult.GetValue(debugOption);

    var chatBot = new ChatBot(envFileOptionValue!);
    await chatBot.RunAsync(debugOptionValue);
});

rootCommand.Add(chatCommand);

var parseResult = rootCommand.Parse(args);

await parseResult.InvokeAsync();
