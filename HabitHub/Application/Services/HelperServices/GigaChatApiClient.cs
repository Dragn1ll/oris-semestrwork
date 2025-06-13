using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Enums;
using Application.Interfaces.Services;
using Application.Interfaces.Services.HelperServices;
using Application.Utils;
using Microsoft.Extensions.Configuration;

namespace Application.Services.HelperServices;

public class GigaChatApiClient : IGigaChatApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public GigaChatApiClient(IConfiguration config)
    {
        _config = config;
        
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler);
    }
    
    public async Task<Result<string>> GetAccessTokenAsync()
    {
        try
        {
            var authUrl = _config["GIGACHAT_AUTH_URL"];
            var scope = _config["GIGACHAT_SCOPE"];
            var clientId = _config["GIGACHAT_CLIENT_ID"];
            var clientSecret = _config["GIGACHAT_CLIENT_SECRET"];
            var basicAuth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            
            _httpClient.DefaultRequestVersion = HttpVersion.Version11;

            var request = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("scope", scope!)
                ])
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();

            return Result<string>.Success(token!);
        }
        catch (Exception)
        {
            return Result<string>.Failure(new Error(ErrorType.ServerError,
                "Не удалось получить токен доступа от GigaChatAi"));
        }
    }
    
    public async Task<Result<string>> SendMessageAsync(string accessToken, string message)
    {
        try
        {
            var apiUrl = _config["GIGACHAT_API_URL"];

            var payload = new
            {
                model = "GigaChat:latest",
                messages = new[]
                {
                    new { role = "user", content = message }
                },
                n = 1,
                stream = false,
                max_tokens = 512,
                repetition_penalty = 1,
                update_interval = 0
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(responseContent);
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Result<string>.Success(reply!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке сообщения: {ex}");
            return Result<string>.Failure(new Error(ErrorType.ServerError,
                "Не удалось отправить запрос GigaChatAi"));
        }
    }

}