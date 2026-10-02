using SkillBridge.ApiService.Profiles;

namespace SkillBridge.ApiService.Tests.Profiles;

public class ProfileStrengthTests
{
    [Fact]
    public void An_empty_profile_scores_zero_and_asks_for_a_headline_first()
    {
        var (percent, note) = ProfileStrength.Compute("", "", 0, 0, referencesApply: true);

        Assert.Equal(0, percent);
        Assert.Equal("Add a headline", note);
    }

    [Fact]
    public void A_headline_alone_is_worth_a_quarter_when_references_apply()
    {
        var (percent, note) = ProfileStrength.Compute("Maths tutor", "", 0, 0, referencesApply: true);

        Assert.Equal(25, percent);
        Assert.Equal("Add a short bio", note);
    }

    [Fact]
    public void Whitespace_only_text_does_not_count()
    {
        var (percent, _) = ProfileStrength.Compute("   ", "\t\n", 0, 0, referencesApply: true);

        Assert.Equal(0, percent);
    }

    [Fact]
    public void Null_text_does_not_count()
    {
        var (percent, _) = ProfileStrength.Compute(null, null, 0, 0, referencesApply: true);

        Assert.Equal(0, percent);
    }

    [Theory]
    [InlineData(0, 50, "Add 3 more skills")]
    [InlineData(1, 60, "Add 2 more skills")]
    [InlineData(2, 70, "Add 1 more skill")]
    public void Skills_are_worth_ten_each_with_a_hint_for_the_remainder(int skills, int expectedPercent, string expectedNote)
    {
        var (percent, note) = ProfileStrength.Compute("Headline", "Bio", skills, 0, referencesApply: true);

        Assert.Equal(expectedPercent, percent);
        Assert.Equal(expectedNote, note);
    }

    [Fact]
    public void Skills_beyond_three_add_nothing_more()
    {
        var (three, _) = ProfileStrength.Compute("Headline", "Bio", 3, 0, referencesApply: true);
        var (ten, _) = ProfileStrength.Compute("Headline", "Bio", 10, 0, referencesApply: true);

        Assert.Equal(80, three);
        Assert.Equal(three, ten);
    }

    [Fact]
    public void When_references_apply_a_complete_profile_without_one_asks_for_a_reference()
    {
        var (percent, note) = ProfileStrength.Compute("Headline", "Bio", 3, 0, referencesApply: true);

        Assert.Equal(80, percent);
        Assert.Equal("Add a reference", note);
    }

    [Fact]
    public void A_reference_completes_the_profile_when_references_apply()
    {
        var (percent, note) = ProfileStrength.Compute("Headline", "Bio", 3, 1, referencesApply: true);

        Assert.Equal(100, percent);
        Assert.Null(note);
    }

    [Fact]
    public void Extra_references_do_not_push_the_score_past_one_hundred()
    {
        var (percent, _) = ProfileStrength.Compute("Headline", "Bio", 3, 7, referencesApply: true);

        Assert.Equal(100, percent);
    }

    [Fact]
    public void When_references_do_not_apply_the_other_fields_alone_can_reach_one_hundred()
    {
        var (percent, note) = ProfileStrength.Compute("Headline", "Bio", 3, 0, referencesApply: false);

        Assert.Equal(100, percent);
        Assert.Null(note);
    }

    [Fact]
    public void When_references_do_not_apply_the_score_is_scaled_to_the_remaining_fields()
    {
        var (percent, _) = ProfileStrength.Compute("Headline", "", 0, 0, referencesApply: false);

        Assert.Equal(31, percent);
    }

    [Fact]
    public void References_are_ignored_entirely_when_they_do_not_apply()
    {
        var (without, _) = ProfileStrength.Compute("Headline", "Bio", 1, 0, referencesApply: false);
        var (with, _) = ProfileStrength.Compute("Headline", "Bio", 1, 5, referencesApply: false);

        Assert.Equal(without, with);
    }
}
