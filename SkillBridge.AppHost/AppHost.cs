var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.SkillBridge_ApiService>("skillbridge-apiservice");

builder.AddProject<Projects.SkillBridge>("skillbridge")
    .WithReference(apiService);

builder.AddProject<Projects.SkillBridge_Web>("skillbridge-web")
    .WithReference(apiService);

builder.Build().Run();
