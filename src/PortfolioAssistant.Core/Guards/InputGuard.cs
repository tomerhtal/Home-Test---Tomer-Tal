using System.Text.RegularExpressions;

namespace PortfolioAssistant.Core.Guards;

public record InputCheck(bool Allowed, string? Reason, string? Message)
{
    public static readonly InputCheck Pass = new(true, null, null);
    public static InputCheck Block(string reason, string message) => new(false, reason, message);
}

public interface IInputGuard
{
    Task<InputCheck> CheckAsync(string question, CancellationToken ct);
}

/// <summary>
/// בדיקת קלט לפני המודל. באב-טיפוס: חוקים דטרמיניסטיים (ביטויים שלמים, לא מילים בודדות).
/// בייצור מוסיפים כאן Bedrock Guardrails (נושא אסור + prompt attack), ומספיק שאחד מהם חוסם.
/// </summary>
public sealed partial class InputGuard : IInputGuard
{
    public const int MaxLength = 1000;

    public Task<InputCheck> CheckAsync(string question, CancellationToken ct) => Task.FromResult(Check(question));

    public static InputCheck Check(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > MaxLength)
            return InputCheck.Block("invalid_question", Messages.InvalidQuestion);

        var isAdvice = AdvicePatterns.Any(p => p.IsMatch(question));
        var isInjection = InjectionPatterns.Any(p => p.IsMatch(question));

        if (isAdvice)
            return InputCheck.Block(isInjection ? "injection_and_advice" : "advice_request", Messages.AdviceRefusal);
        if (isInjection)
            return InputCheck.Block("prompt_injection", Messages.CannotComply);

        return InputCheck.Pass;
    }

    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    internal static readonly Regex[] AdvicePatterns =
    [
        new(@"כדאי\s+(\S+\s+){0,3}?(לקנות|למכור|להחזיק|להשקיע|לצאת|להגדיל|להקטין|לממש)", Opt),
        new(@"(המלצת|המלצה|המלצות)\s+(קנייה|קניה|מכירה|השקעה)", Opt),
        new(@"(תמליץ|תמליצי|ממליץ\s+לי|אתה\s+ממליץ|את\s+ממליצה)", Opt),
        new(@"מה\s+(היית|הייתי)\s+(עושה|ממליץ)", Opt),
        new(@"במקומ(ו|ה|י|ם)", Opt),
        new(@"להחזיק\s+או\s+ל(מכור|צאת|ממש)", Opt),
        new(@"שווה\s+(\S+\s+)?(לקנות|למכור|להשקיע)", Opt),
        new(@"should\s+(i|we|he|she|they)\s+(buy|sell|hold)", Opt),
        new(@"recommend", Opt)
    ];

    internal static readonly Regex[] InjectionPatterns =
    [
        new(@"(התעלם|תתעלם|תתעלמי)\s+מ", Opt),
        new(@"(שכח|תשכח|תשכחי)\s+(את\s+)?(כל\s+)?ה(הוראות|כללים|הנחיות)", Opt),
        new(@"ההוראות\s+הקודמות", Opt),
        new(@"אתה\s+עכשיו", Opt),
        new(@"ignore\s+(all\s+|any\s+|the\s+)?(previous|prior|above)", Opt),
        new(@"system\s+prompt", Opt),
        new(@"you\s+are\s+now", Opt)
    ];
}
