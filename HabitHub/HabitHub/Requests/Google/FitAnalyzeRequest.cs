using System.ComponentModel.DataAnnotations;

namespace HabitHub.Requests.Google;

public record FitAnalyzeRequest(
    [Required] string Goal);