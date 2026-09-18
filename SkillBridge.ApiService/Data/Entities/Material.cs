namespace SkillBridge.ApiService.Data;

public enum MaterialStatus
{
    Draft,
    Published,
}

// CourseTitle isn't stored here — a tutor's materials are always looked up alongside their own
// courses in the same request, so the endpoint joins CourseId against that in-memory list
// rather than duplicating the title on every material row.
public class Material
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public required string Title { get; set; }
    public MaterialStatus Status { get; set; }
}
