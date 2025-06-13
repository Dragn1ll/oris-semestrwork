using System.Text;
using System.Text.Json;
using Application.Dto_s.Fit;
using Application.Enums;
using Application.Interfaces.Services;
using Application.Interfaces.Services.HelperServices;
using Application.Utils;
using Domain.Models;

namespace Application.Services.HelperServices;

public class GigaChatAiService(IGigaChatApiClient gigaChatClient) : IAiService
{
    public async Task<Result<GoalAnalysisDto>> AnalyzeGoalCompletionAsync(
        ICollection<ActivityData> activities,
        string userGoal)
    {
        if (string.IsNullOrWhiteSpace(userGoal))
        {
            return Result<GoalAnalysisDto>.Failure(
                new Error(ErrorType.ServerError, "Цель не может быть пустой"));
        }

        if (activities == null! || activities.Count == 0)
        {
            return Result<GoalAnalysisDto>.Failure(
                new Error(ErrorType.ServerError, "Нет данных активности для анализа"));
        }

        try
        {
            var today = DateTime.UtcNow;
            var todayActivities = activities
                .Where(a => a.StartTime.Date == today)
                .ToList();
            var activitySummary = PrepareActivitySummary(todayActivities);

            var prompt = $$"""
                Анализ выполнения дневной цели пользователя (дата: {{today:dd.MM.yyyy}}):

                **Цель:** "{{userGoal}}"

                **Сегодняшняя активность:**
                {{activitySummary}}

                **Требования к ответу:**
                1. Рассчитай процент выполнения цели (0-100%)
                2. Сравни фактические показатели с целью
                3. Укажи основные достижения
                4. Дай рекомендации по улучшению

                **Формат ответа (строгий JSON):**
                {
                    "completionPercentage": number,
                    "analysisSummary": string,
                    "metrics": {
                        "steps": number,
                        "calories": number,
                        "distanceKm": number,
                        "mainActivity": string,
                        "activeMinutes": number
                    }
                }
                """;

            var accessToken = await gigaChatClient.GetAccessTokenAsync();
            if (!accessToken.IsSuccess)
                return Result<GoalAnalysisDto>.Failure(accessToken.Error);
            
            var responseText = await gigaChatClient.SendMessageAsync(accessToken.Value!, prompt);
            if (!responseText.IsSuccess)
                return Result<GoalAnalysisDto>.Failure(responseText.Error);
            
            var result = ParseAiResponse(responseText.Value!);
            return Result<GoalAnalysisDto>.Success(result);
        }
        catch (JsonException)
        {
            return Result<GoalAnalysisDto>.Failure(
                new Error(ErrorType.ServerError, "Неверный формат ответа от AI"));
        }
        catch (Exception)
        {
            return Result<GoalAnalysisDto>.Failure(
                new Error(ErrorType.ServerError, "Ошибка анализа целей"));
        }
    }

    private string PrepareActivitySummary(ICollection<ActivityData> activities)
    {
        var summary = new StringBuilder();
        var totalSteps = activities.Sum(a => a.Steps);
        var totalCalories = activities.Sum(a => a.Calories);
        var totalDistance = activities.Sum(a => a.Distance) / 1000;
        var totalMinutes = activities.Sum(a => (a.EndTime - a.StartTime).Minutes);
        var mainActivity = activities.MaxBy(a => (a.EndTime - a.StartTime).Minutes);

        summary.AppendLine($"- Общее количество шагов: {totalSteps}");
        summary.AppendLine($"- Сожжено калорий: {totalCalories} ккал");
        summary.AppendLine($"- Пройдено дистанции: {totalDistance:F2} км");
        summary.AppendLine($"- Общее время активности: {totalMinutes} мин");

        if (mainActivity != null)
        {
            summary.AppendLine($"- Основная активность: {mainActivity.ActivityType} " +
                               $"({(mainActivity.EndTime - mainActivity.StartTime).Minutes} мин)");
        }

        return summary.ToString();
    }

    private GoalAnalysisDto ParseAiResponse(string aiResponse)
    {
        var jsonStart = aiResponse.IndexOf('{');
        var jsonEnd = aiResponse.LastIndexOf('}') + 1;
        var cleanJson = jsonStart >= 0 && jsonEnd > jsonStart 
            ? aiResponse[jsonStart..jsonEnd] 
            : aiResponse;

        var result = JsonSerializer.Deserialize<GoalAnalysisDto>(
            cleanJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? throw new JsonException("Не удалось десериализовать ответ AI");
    }
}