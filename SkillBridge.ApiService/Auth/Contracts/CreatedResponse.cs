namespace SkillBridge.ApiService.Auth.Contracts;

// Shared minimal "created" response, reused by Courses/Jobs endpoints the same way
// ErrorResponse already is — not auth-specific despite the namespace.
public record CreatedResponse(Guid Id);
