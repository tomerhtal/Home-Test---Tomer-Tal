namespace PortfolioAssistant.Core.Guards;

/// <summary>
/// כל הנוסחים הקבועים במקום אחד. בייצור הם מאושרים על ידי מחלקת הציות, והמודל לא מנסח אותם.
/// </summary>
public static class Messages
{
    public const string AdviceRefusal =
        "אינני יכול לתת המלצות קנייה, מכירה או החזקה של נכסים. לשאלות מסוג זה יש לפנות ליועץ השקעות מורשה. " +
        "אשמח לעזור במידע על התיק או על מדיניות המוצר.";

    public const string CustomerNotAvailable =
        "לא נמצא לקוח זמין עבורך. אם מדובר בלקוח שבאחריותך, יש לפנות לתמיכה.";

    public const string NoInformation =
        "לא מצאתי מידע על כך במקורות הפנימיים.";

    public const string CouldNotAnswer =
        "לא הצלחתי לענות על השאלה. אפשר לנסות לנסח אותה אחרת.";

    public const string AssistantUnavailable =
        "העוזר אינו זמין כרגע. אפשר להמשיך לעבוד במערכת התיקים כרגיל.";

    public const string DataUnavailable =
        "לא ניתן היה לקבל נתונים עדכניים ממערכות המקור כרגע, ולכן לא אציג מספרים. אפשר לנסות שוב בעוד כמה דקות.";

    public const string CannotComply =
        "אינני יכול לבצע את הבקשה הזו. אשמח לעזור במידע על התיק, על מסמכי המדיניות ועל סקירות המחקר.";

    public const string InvalidQuestion =
        "השאלה ריקה או ארוכה מדי. אפשר לנסח שאלה קצרה יותר.";
}
