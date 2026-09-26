namespace SkillBridge.Services;

public static class ApiConfig
{
    // The real, publicly reachable ApiService (deployed via Render's Docker web service —
    // see /Dockerfile.apiservice at the repo root; Render assigns the actual *.onrender.com
    // URL once the service is created). MAUI isn't part of Aspire's service-discovery graph
    // the way the Web head is, so this has always needed to be a real reachable address rather
    // than a "skillbridge-apiservice" service name — every platform (Windows, Android, iOS)
    // points at it directly, which is also what makes a distributed EXE/APK actually work off
    // this machine.
    // TODO: replace with the real Render URL once the ApiService web service exists, then
    // rebuild the Windows EXE / Android APK (same cutover this const went through for Fly.io).
    public const string BaseUrl = "https://REPLACE-WITH-RENDER-API-URL.onrender.com";
}
