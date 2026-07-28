using DotMake.CommandLine;
using dotkit.Commands;

Cli.GetArgs();
await Cli.RunAsync<RootCommand>(args);
