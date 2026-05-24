public enum ContextType
{
    Diplomatic,
    Scientific,
    Literary,
    Casual
}

public record AlienTranslationWorkflowInput(
    AlienText AlienText,
    List<Translation> Translations,
    List<Evaluation> Evaluations
);

public record AlienText(
    string TextId,
    string AlienSpecies,
    string OriginalText,
    ContextType Context,
    Dictionary<string, string> KnownVocabulary,
    string CulturalNotes
);

public record Translation(
    int IterationNumber,
    string TranslatedText,
    string TranslatorReasoning,
    DateTime Timestamp
);

public record Evaluation(
    int IterationNumber,
    double AccuracyScore,
    double CulturalNuanceScore,
    double IdiomaticScore,
    double OverallQuality,
    List<string> Strengths,
    List<string> Weaknesses,
    string DetailedFeedback,
    bool MeetsStandards
);

public record AlienTranslationWorkflowOutput(
    string TextId,
    List<Translation> Translations,
    List<Evaluation> Evaluations,
    Translation FinalTranslation,
    Evaluation FinalEvaluation,
    int TotalIterations,
    string ImprovementSummary
);

public record TranslateInput(AlienText Text, Evaluation? PreviousFeedback, int Iteration);
public record EvaluateInput(AlienText OriginalText, Translation CurrentTranslation);
public record RefineInput(AlienText Text, Translation Current, Evaluation Feedback, int Iteration);
