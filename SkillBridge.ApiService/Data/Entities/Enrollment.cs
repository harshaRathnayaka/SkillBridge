namespace SkillBridge.ApiService.Data;

// Links a real Student to a real Course row (CourseId is a genuine FK — Course.TeacherName is
// reached via that join, no denormalization needed here). Powers both the student's own
// "active courses" list and, via a count query on CourseId, the teaching side's
// "learners enrolled" stat.
public class Enrollment
{
    public Guid Id { get; set; }
    public required string StudentId { get; set; }
    public Guid CourseId { get; set; }
    public int LessonsTotal { get; set; }
    public int LessonsRemaining { get; set; }
    public int ProgressPercent { get; set; }
    public required string NextLessonTitle { get; set; }
}
