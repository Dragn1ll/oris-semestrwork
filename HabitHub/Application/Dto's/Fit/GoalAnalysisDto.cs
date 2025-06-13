namespace Application.Dto_s.Fit;

public class GoalAnalysisDto
{
    public int CompletionPercentage { get; set; }
    public string AnalysisSummary { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}