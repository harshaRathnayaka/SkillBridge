namespace SkillBridge.Services;

public static class ApiConfig
{
    // Dev-only default matching SkillBridge.ApiService's https launch profile
    // (SkillBridge.ApiService/Properties/launchSettings.json). MAUI isn't part of Aspire's
    // service-discovery graph the way the Web head is, so this has to be a real reachable
    // address rather than a "skillbridge-apiservice" service name. Android emulators can't
    // reach the host machine via "localhost" — use 10.0.2.2 there instead.
    public const string BaseUrl =
#if ANDROID
        "https://10.0.2.2:7246";
#else
        "https://localhost:7246";
#endif
}
