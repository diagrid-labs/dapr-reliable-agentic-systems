using Dapr.Workflow;

public class AlienTranslationWorkflow : Workflow<AlienTranslationWorkflowInput, AlienTranslationWorkflowOutput>
{
    private const int MAX_ITERATIONS = 5;
    private const double QUALITY_THRESHOLD = 8.0;

    public override async Task<AlienTranslationWorkflowOutput> RunAsync(
        WorkflowContext context,
        AlienTranslationWorkflowInput input)
    {
        Translation translation;
        if (input.Evaluations.Count == 0)
        {
            translation = await context.CallActivityAsync<Translation>(
                nameof(TranslateActivity),
                new TranslateInput(input.AlienText, null, 0));

        } else
        {
            translation = input.RefinedTranslation;
        }

        var evaluation = await context.CallActivityAsync<Evaluation>(
            nameof(EvaluateTranslationActivity),
            new EvaluateInput(input.AlienText, translation));

        var evaluations = input.Evaluations.Append(evaluation).ToList();

        if (evaluation.MeetsStandards && evaluation.OverallQuality >= QUALITY_THRESHOLD)
        {
            return new AlienTranslationWorkflowOutput(
                input.AlienText.TextId,
                evaluations,
                translation,
                evaluation,
                input.Evaluations.Count + 1,
                GenerateImprovementSummary(evaluations)
            );
        }

        if (evaluations.Count == MAX_ITERATIONS - 1)
        {
            return new AlienTranslationWorkflowOutput(
                input.AlienText.TextId,
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
            var refinedTranslation = await context.CallActivityAsync<Translation>(
                nameof(RefineTranslationActivity),
                new RefineInput(
                    input.AlienText,
                    translation,
                    evaluation,
                    evaluations.Count + 1));

            // add refined translation and evaluation to new record 
            input = new AlienTranslationWorkflowInput(
                input.AlienText,
                evaluations,
                refinedTranslation);

            context.ContinueAsNew(input);
        }

        return null; // This line will never be reached
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
