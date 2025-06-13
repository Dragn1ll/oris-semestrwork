using System.ComponentModel.DataAnnotations;

namespace HabitHub.Requests.Google;

public record AddTokenRequest(
    [Required]string AccessToken);