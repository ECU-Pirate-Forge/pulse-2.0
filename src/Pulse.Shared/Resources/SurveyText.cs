using System.Globalization;
using System.Resources;

namespace Pulse.Shared.Resources;

/// <summary>Provides localized labels and messages for survey results and reset actions.</summary>
public static class SurveyText
{
    private static readonly ResourceManager Resources = new(typeof(SurveyText));

    public static string CorrectAnswers => Get(nameof(CorrectAnswers));
    public static string ResetSurvey => Get(nameof(ResetSurvey));
    public static string ResetConfirmation => Get(nameof(ResetConfirmation));
    public static string ConfirmReset => Get(nameof(ConfirmReset));
    public static string CancelReset => Get(nameof(CancelReset));
    public static string ResetFailed => Get(nameof(ResetFailed));
    public static string SurveyOnlyReset => Get(nameof(SurveyOnlyReset));
    public static string EditSurvey => Get(nameof(EditSurvey));
    public static string EditHelp => Get(nameof(EditHelp));
    public static string EditConfirmation => Get(nameof(EditConfirmation));
    public static string ConfirmEdit => Get(nameof(ConfirmEdit));
    public static string EditFailed => Get(nameof(EditFailed));
    public static string SurveyOnlyEdit => Get(nameof(SurveyOnlyEdit));
    public static string EditingReady => Get(nameof(EditingReady));
    public static string SurveyPaused => Get(nameof(SurveyPaused));
    public static string SurveyPausedHelp => Get(nameof(SurveyPausedHelp));
    public static string SubmissionChanged => Get(nameof(SubmissionChanged));

    private static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing survey resource '{name}'.");
}
