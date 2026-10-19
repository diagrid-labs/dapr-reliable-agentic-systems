using Dapr.AI.Conversation;

namespace SpaceDebrisAgent.Models;

public record TokenUsage(long PromptTokens, long CompletionTokens, long TotalTokens)
{
    public static TokenUsage Zero { get; } = new(0, 0, 0);

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.PromptTokens + b.PromptTokens, a.CompletionTokens + b.CompletionTokens, a.TotalTokens + b.TotalTokens);
}

public record Metered<T>(T Value, TokenUsage Usage);

public static class ConversationResponseExtensions
{
    public static TokenUsage ToUsage(this ConversationResponse response) =>
        response.Outputs
            .Select(o => o.Usage)
            .Where(u => u is not null)
            .Aggregate(TokenUsage.Zero, (sum, u) =>
                sum + new TokenUsage(Saturate(u!.PromptTokens), Saturate(u.CompletionTokens), Saturate(u.TotalTokens)));

    private static long Saturate(ulong value) => (long)Math.Min(value, long.MaxValue);
}
