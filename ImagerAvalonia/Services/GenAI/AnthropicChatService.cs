using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services.GenAI
{
    public class AnthropicChatResult
    {
        public bool Success { get; set; }
        public string? AssistantText { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IAnthropicChatService
    {
        Task<AnthropicChatResult> SendMessageAsync(string systemPrompt, JArray messages, CancellationToken cancellationToken = default);
    }

    public class AnthropicChatService : IAnthropicChatService
    {
        private const string ApiKeyEnvironmentVariable = "ANTHROPIC_API_KEY";
        private const string Model = "claude-opus-5";
        private const int MaxTokens = 8192;
        private const string AnthropicVersion = "2023-06-01";

        private readonly HttpClient _httpClient;

        public AnthropicChatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<AnthropicChatResult> SendMessageAsync(string systemPrompt, JArray messages, CancellationToken cancellationToken = default)
        {
            var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = $"The {ApiKeyEnvironmentVariable} environment variable is not set on this machine. " +
                                   "Set it (to a valid Anthropic API key) and restart the application to use the AI assistant."
                };
            }

            var body = new JObject
            {
                ["model"] = Model,
                ["max_tokens"] = MaxTokens,
                ["system"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt,
                        ["cache_control"] = new JObject { ["type"] = "ephemeral" }
                    }
                },
                ["messages"] = messages
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
            {
                Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = $"Could not reach the Anthropic API: {ex.Message}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string detail = responseBody;
                try
                {
                    var errorJson = JObject.Parse(responseBody);
                    detail = errorJson["error"]?["message"]?.ToString() ?? responseBody;
                }
                catch (JsonException)
                {
                    detail = responseBody;
                }

                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = $"Anthropic API error ({(int)response.StatusCode} {response.StatusCode}): {detail}"
                };
            }

            JObject responseJson;
            try
            {
                responseJson = JObject.Parse(responseBody);
            }
            catch (JsonException ex)
            {
                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = $"Could not parse the Anthropic API response: {ex.Message}"
                };
            }

            var stopReason = responseJson["stop_reason"]?.ToString();
            if (stopReason == "refusal")
            {
                var category = responseJson["stop_details"]?["category"]?.ToString() ?? "unspecified";
                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = $"The request was declined by Anthropic's safety systems (category: {category})."
                };
            }

            var contentArray = responseJson["content"] as JArray;
            var textBlock = contentArray?.FirstOrDefault(b => b["type"]?.ToString() == "text");
            var text = textBlock?["text"]?.ToString();

            if (string.IsNullOrEmpty(text))
            {
                return new AnthropicChatResult
                {
                    Success = false,
                    ErrorMessage = "The Anthropic API returned no text content."
                };
            }

            return new AnthropicChatResult
            {
                Success = true,
                AssistantText = text
            };
        }
    }
}
