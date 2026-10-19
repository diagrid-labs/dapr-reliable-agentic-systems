using Dapr.Workflow;

public class AlienTranslationWorkflow : Workflow<AlienTranslationWorkflowInput, AlienTranslationWorkflowOutput>
{
    private const int MAX_ITERATIONS = 5;
    private const double QUALITY_THRESHOLD = 9.0;

    private static WorkflowTaskOptions GetDefaultRetryPolicy()
    {
        return new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));
    }

    public override async Task<AlienTranslationWorkflowOutput> RunAsync(
        WorkflowContext context,
        AlienTranslationWorkflowInput input)
    {
        // Republish history so the status is not empty right after ContinueAsNew
        var iterations = input.Translations
            .Zip(input.Evaluations)
            .Select((x, i) => new IterationProgress(
                i + 1, x.First.TranslatedText, x.Second.OverallQuality, x.Second.MeetsStandards))
            .ToList();
        var usage = input.Usage ?? TokenUsage.Zero;
        if (iterations.Count > 0)
        {
            context.SetCustomStatus(new TranslationProgress(iterations.ToList(), QUALITY_THRESHOLD, MAX_ITERATIONS, usage));
        }

        Metered<Translation> translated;
        if (input.Evaluations.Count == 0)
        {
            translated = await context.CallActivityAsync<Metered<Translation>>(
                nameof(TranslateActivity),
                new TranslateInput(input.AlienText, null, input.Evaluations.Count + 1),
                GetDefaultRetryPolicy());

        }
        else
        {
            translated = await context.CallActivityAsync<Metered<Translation>>(
                nameof(RefineTranslationActivity),
                new RefineInput(
                    input.AlienText,
                    input.Translations.Last(),
                    input.Evaluations.Last(),
                    input.Evaluations.Count + 1),
                GetDefaultRetryPolicy());
        }

        var translation = translated.Value;
        usage += translated.Usage;
        context.SetCustomStatus(new TranslationProgress(iterations.ToList(), QUALITY_THRESHOLD, MAX_ITERATIONS, usage));

        var translations = input.Translations.Append(translation).ToList();

        var evaluated = await context.CallActivityAsync<Metered<Evaluation>>(
            nameof(EvaluateTranslationActivity),
            new EvaluateInput(input.AlienText, translation),
            GetDefaultRetryPolicy());

        var evaluation = evaluated.Value;
        usage += evaluated.Usage;
        var evaluations = input.Evaluations.Append(evaluation).ToList();
        iterations.Add(new IterationProgress(
            iterations.Count + 1, translation.TranslatedText, evaluation.OverallQuality, evaluation.MeetsStandards));
        context.SetCustomStatus(new TranslationProgress(iterations.ToList(), QUALITY_THRESHOLD, MAX_ITERATIONS, usage));

        if (evaluation.MeetsStandards && evaluation.OverallQuality >= QUALITY_THRESHOLD)
        {
            return new AlienTranslationWorkflowOutput(
                input.AlienText.TextId,
                translations,
                evaluations,
                translation,
                evaluation,
                evaluations.Count,
                GenerateImprovementSummary(evaluations)
            );
        }

        if (evaluations.Count == MAX_ITERATIONS)
        {
            return new AlienTranslationWorkflowOutput(
                input.AlienText.TextId,
                translations,
                evaluations,
                translation,
                evaluation,
                MAX_ITERATIONS,
                GenerateImprovementSummary(evaluations) +
                " (Max iterations reached - manual review recommended)"
            );
        }
        else
        {
            // Continue to next iteration
            input = input with {
                 Translations = translations,
                 Evaluations = evaluations,
                 Usage = usage
            };

            context.ContinueAsNew(input);
        }

        // ContinueAsNew restarts the workflow, so this return value is never used
        return null!;
    }

    private string GenerateImprovementSummary(List<Evaluation> evaluations)
    {
        if (evaluations.Count < 2)
            return "Initial translation completed";

        var firstScore = evaluations.First().OverallQuality;
        var lastScore = evaluations.Last().OverallQuality;
        var improvement = lastScore - firstScore;

        return $"Quality improved from {firstScore:F1} to {lastScore:F1} " +
               $"(+{improvement:F1} points over {evaluations.Count} evaluations)";
    }
}
