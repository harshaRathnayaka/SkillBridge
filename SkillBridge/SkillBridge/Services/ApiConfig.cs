namespace SkillBridge.Services;

public static class ApiConfig
{
    // The real, publicly reachable ApiService (deployed to Fly.io — see
    // /fly.apiservice.toml and /Dockerfile.apiservice at the repo root). MAUI isn't part of
    // Aspire's service-discovery graph the way the Web head is, so this has always needed to
    // be a real reachable address rather than a "skillbridge-apiservice" service name — now
    // that there's a real deployment, every platform (Windows, Android, iOS) points at it
    // directly, which is also what makes a distributed EXE/APK actually work off this machine.
    public const string BaseUrl = "https://skillbridge-api.fly.dev";
}
