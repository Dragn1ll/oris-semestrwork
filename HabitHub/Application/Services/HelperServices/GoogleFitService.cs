using Application.Enums;
using Application.Interfaces.Services;
using Application.Interfaces.Services.HelperServices;
using Application.Utils;
using Domain.Enums;
using Domain.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Fitness.v1;
using Google.Apis.Fitness.v1.Data;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services.HelperServices;

public class GoogleFitService(
    IConfiguration configuration,
    IGoogleTokenStore tokenStore,
    ILogger<GoogleFitService> logger)
    : IGoogleFitService
{
    public async Task<Result<ICollection<ActivityData>>> GetActivityDataAsync(Guid userId,
    DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return Result<ICollection<ActivityData>>.Failure(
                new Error(ErrorType.BadRequest, "Дата начала должна быть раньше даты окончания"));
        }

        try
        {
            var credentialsResult = await TryGetUserCredentialsAsync(userId);
            if (!credentialsResult.IsSuccess)
            {
                return Result<ICollection<ActivityData>>.Failure(credentialsResult.Error!);
            }

            var credentials = credentialsResult.Value!;
            var service = new FitnessService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials,
                ApplicationName = configuration["GoogleFit:ApplicationName"]
            });
            
            var dataSources = await service.Users.DataSources.List("me").ExecuteAsync();
            if (dataSources.DataSource == null || !dataSources.DataSource.Any())
            {
                logger.LogWarning("Нет зарегистрированных устройств или источников данных");
                return Result<ICollection<ActivityData>>.Failure(
                    new Error(ErrorType.NotFound, 
                        "Нет зарегистрированных устройств. " +
                        "Пожалуйста, подключите фитнес-трекер в Google Fit."));
            }
            
            var availableDataTypes = dataSources.DataSource
                .Select(d => d.DataType.Name)
                .ToList();
            var aggregateBy = new List<AggregateBy>();

            var requestedTypes = new[]
            {
                "com.google.activity.summary",
                "com.google.step_count.delta",
                "com.google.calories.expended",
                "com.google.distance.delta"
            };

            foreach (var type in requestedTypes)
            {
                if (availableDataTypes.Contains(type))
                {
                    aggregateBy.Add(new AggregateBy { DataTypeName = type });
                }
                else
                {
                    logger.LogWarning($"Тип данных {type} недоступен");
                }
            }

            if (!aggregateBy.Any())
            {
                logger.LogWarning("Нет доступных типов данных для запроса");
                return Result<ICollection<ActivityData>>.Failure(
                    new Error(ErrorType.NotFound, 
                        "Нет доступных данных. Пожалуйста, проверьте настройки Google Fit."));
            }

            var request = new AggregateRequest
            {
                AggregateBy = aggregateBy,
                BucketByTime = new BucketByTime { DurationMillis = 86_400_000 },
                StartTimeMillis = ToUnixMillis(startDate),
                EndTimeMillis = ToUnixMillis(endDate)
            };

            logger.LogInformation($"Отправляется запрос к Google Fit с {startDate} по {endDate}");
            var response = await service.Users.Dataset.Aggregate(request, "me").ExecuteAsync();

            if (response?.Bucket == null)
            {
                logger.LogInformation("Ответ от Google Fit пустой (bucket = null)");
                return Result<ICollection<ActivityData>>.Success(Array.Empty<ActivityData>());
            }

            logger.LogInformation($"Получено {response.Bucket.Count} bucket'ов от Google Fit");

            var result = new List<ActivityData>();

            foreach (var bucket in response.Bucket)
            {
                var activity = new ActivityData
                {
                    StartTime = FromUnixMillis(bucket.StartTimeMillis ?? 0),
                    EndTime = FromUnixMillis(bucket.EndTimeMillis ?? 0),
                    ActivityType = PhysicalActivityType.Other,
                    Steps = 0,
                    Calories = 0,
                    Distance = 0
                };

                if (bucket.Dataset == null) continue;

                foreach (var dataset in bucket.Dataset)
                {
                    if (dataset.Point == null) continue;

                    foreach (var point in dataset.Point)
                    {
                        if (point.Value == null || point.Value.Count == 0) continue;

                        switch (point.DataTypeName)
                        {
                            case "com.google.activity.summary":
                                var intVal = point.Value.FirstOrDefault()?.IntVal ?? 0;
                                activity.ActivityType = GetActivityType(intVal);
                                break;

                            case "com.google.step_count.delta":
                                activity.Steps += point.Value.FirstOrDefault()?.IntVal ?? 0;
                                break;

                            case "com.google.calories.expended":
                                var calories = point.Value.FirstOrDefault()?.FpVal ?? 0;
                                activity.Calories += (int)Math.Round(calories);
                                break;

                            case "com.google.distance.delta":
                                activity.Distance += point.Value.FirstOrDefault()?.FpVal ?? 0;
                                break;
                        }
                    }
                }

                result.Add(activity);
            }

            logger.LogInformation("Успешно получены и обработаны данные от Google Fit");
            return Result<ICollection<ActivityData>>.Success(result);
        }
        catch (Google.GoogleApiException ex) when (ex.Error.Code == 429)
        {
            logger.LogError(ex, "Превышена квота Google Fit API");
            return Result<ICollection<ActivityData>>.Failure(
                new Error(ErrorType.ServerError, "Превышена квота Google Fit API"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при запросе к Google Fit");
            return Result<ICollection<ActivityData>>.Failure(
                new Error(ErrorType.ServerError, "Ошибка при получении данных из Google Fit"));
        }
    }

    private async Task<Result<ICredential>> TryGetUserCredentialsAsync(Guid userId)
    {
        var tokenResult = await tokenStore.GetTokenAsync(userId);
        if (!tokenResult.IsSuccess || tokenResult.Value == null)
        {
            return Result<ICredential>.Failure(
                new Error(ErrorType.BadRequest, "Требуется авторизация в Google"));
        }

        var tokenData = tokenResult.Value;

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = configuration["Google:ClientId"],
                ClientSecret = configuration["Google:ClientSecret"]
            },
            Scopes = [FitnessService.Scope.FitnessActivityRead],
            HttpClientFactory = new HttpClientFactory(),
            DataStore = new FileDataStore("GoogleAuthStore")
        });

        var credential = new UserCredential(flow, userId.ToString(), new TokenResponse
        {
            AccessToken = tokenData.AccessToken,
            RefreshToken = tokenData.RefreshToken,
            ExpiresInSeconds = (long)(tokenData.ExpiresAt - DateTime.UtcNow).TotalSeconds
        });

        if (!credential.Token.IsExpired(SystemClock.Default)) return Result<ICredential>.Success(credential);
        try
        {
            if (!await credential.RefreshTokenAsync(CancellationToken.None))
            {
                return Result<ICredential>.Failure(
                    new Error(ErrorType.BadRequest, "Не удалось обновить токен"));
            }

            var updateResult = await tokenStore.UpdateTokenAsync(
                userId,
                credential.Token.AccessToken,
                credential.Token.RefreshToken,
                DateTime.UtcNow.AddSeconds(credential.Token.ExpiresInSeconds ?? 3600));

            if (!updateResult.IsSuccess)
            {
                logger.LogError($"Не удалось сохранить обновлённый токен: {updateResult.Error?.Message}");
            }
        }
        catch (TokenResponseException ex)
        {
            logger.LogError(ex, "Ошибка обновления Google токена");
            await tokenStore.RemoveTokenAsync(userId);
            return Result<ICredential>.Failure(
                new Error(ErrorType.BadRequest, "Требуется повторная авторизация"));
        }

        return Result<ICredential>.Success(credential);
    }

    private static long ToUnixMillis(DateTime date) =>
        (long)(date.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;

    private static DateTime FromUnixMillis(long millis) =>
        DateTime.UnixEpoch.AddMilliseconds(millis);

    private static PhysicalActivityType GetActivityType(int activityType) => activityType switch
    {
        7 => PhysicalActivityType.Walking,
        8 => PhysicalActivityType.Running,
        9 => PhysicalActivityType.Cycling,
        10 => PhysicalActivityType.Swimming,
        11 => PhysicalActivityType.Skiing,
        12 => PhysicalActivityType.Snowboarding,
        83 => PhysicalActivityType.Yoga,
        _ => PhysicalActivityType.Other
    };
}