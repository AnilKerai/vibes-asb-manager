var builder = DistributedApplication.CreateBuilder(args);


var postgres = 
       builder
              .AddPostgres("postgres")
              .WithDataVolume()
              ;
var asbdb = 
       postgres
              .AddDatabase("asbdb");

builder
       .AddProject<Projects.Vibes_ASBManager_Web>("vibes-asbmanager-web")
       .WithReference(asbdb)
       .WaitFor(asbdb)
       ;

builder.Build().Run();
