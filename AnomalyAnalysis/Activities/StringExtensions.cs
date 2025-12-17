public static class StringExtensions
{
    public static string FormatToMarkDown(this string message, int wordLength)
    {
        return $"{message.Trim()}. The output type should be markdown. Do not include em-dashes or backticks. Limit the output to {wordLength} words.";
    }

    public static string FormatToJson(this string message, int wordLength)
    {
        return $"{message.Trim()}. The output type should be json. Do not include em-dashes or backticks. Limit the output to {wordLength} words.";
    }
}