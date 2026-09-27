namespace SkillBridge.Services;

public static class ApiConfig
{
    // The real, publicly reachable ApiService (deployed via SnapDeploy from the image CI builds
    // and pushes to ghcr.io/<owner>/skillbridge-api — see /Dockerfile.apiservice at the repo root
    // and the build-and-push-images CI job). MAUI isn't part of Aspire's service-discovery graph
    // the way the Web head is, so this has always needed to be a real reachable address rather
    // than a "skillbridge-apiservice" service name — every platform (Windows, Android, iOS)
    // points at it directly, which is also what makes a distributed EXE/APK actually work off
    // this machine.
    // TODO: replace with the real SnapDeploy container URL once it exists, then rebuild the
    // Windows EXE / Android APK (same cutover this const went through for Fly.io and Render).
    public const string BaseUrl = "https://REPLACE-WITH-SNAPDEPLOY-API-URL";
}
