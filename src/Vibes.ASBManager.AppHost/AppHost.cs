var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.Vibes_ASBManager_Web>("vibes-asbmanager-web");

builder.Build().Run();
