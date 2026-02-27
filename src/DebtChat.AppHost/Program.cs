var builder = DistributedApplication.CreateBuilder(args);

var console = builder.AddProject<Projects.DebtChat_Console>("debtchat-console");

builder.Build().Run();
