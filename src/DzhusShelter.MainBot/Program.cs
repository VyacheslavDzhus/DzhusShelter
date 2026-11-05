// Placeholder entry point so the Worker SDK compiles; Task 8 replaces it with the real wiring.
var builder = Host.CreateApplicationBuilder(args);
await builder.Build().RunAsync();
