using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalSync.Services
{
    public interface IPayMongoService
    {
        Task<PayMongoCheckoutResponse?> CreateCheckoutSessionAsync(
            decimal amountPhp,
            string planName,
            string billingCycle,
            string fullName,
            string email,
            string successUrl,
            string cancelUrl,
            string? paymentMethod = null);

        Task<PayMongoCheckoutResponse?> GetCheckoutSessionAsync(string sessionId);
    }

    public class PayMongoService : IPayMongoService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayMongoService> _logger;

        public PayMongoService(HttpClient httpClient, IConfiguration configuration, ILogger<PayMongoService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            var baseUrl = _configuration["PayMongo:BaseUrl"] ?? "https://api.paymongo.com/v1/";
            if (!baseUrl.EndsWith("/"))
            {
                baseUrl += "/";
            }
            _httpClient.BaseAddress = new Uri(baseUrl);

            var secretKey = _configuration["PayMongo:SecretKey"] ?? "";
            var authBytes = Encoding.UTF8.GetBytes($"{secretKey}:");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<PayMongoCheckoutResponse?> CreateCheckoutSessionAsync(
            decimal amountPhp,
            string planName,
            string billingCycle,
            string fullName,
            string email,
            string successUrl,
            string cancelUrl,
            string? paymentMethod = null)
        {
            try
            {
                // Amount in centavos (e.g. 799 PHP -> 79900 centavos)
                long amountInCentavos = Convert.ToInt64(amountPhp * 100);

                var methodTypes = string.IsNullOrWhiteSpace(paymentMethod)
                    ? new[] { "card", "gcash", "paymaya" }
                    : new[] { paymentMethod.ToLower() };

                var requestBody = new
                {
                    data = new
                    {
                        attributes = new
                        {
                            send_email_receipt = true,
                            show_description = true,
                            show_line_items = true,
                            cancel_url = cancelUrl,
                            success_url = successUrl,
                            description = $"DentalSync Clinic Subscription ({planName} - {billingCycle})",
                            line_items = new[]
                            {
                                new
                                {
                                    currency = "PHP",
                                    amount = amountInCentavos,
                                    description = $"{planName} ({billingCycle} billing)",
                                    name = $"DentalSync - {planName}",
                                    quantity = 1
                                }
                            },
                            payment_method_types = methodTypes,
                            metadata = new Dictionary<string, string>
                            {
                                { "full_name", fullName ?? "" },
                                { "email", email ?? "" },
                                { "plan", planName ?? "" },
                                { "billing", billingCycle ?? "" }
                            }
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _logger.LogInformation("Creating PayMongo Checkout Session for {Email}, Amount: ₱{Amount}", email, amountPhp);

                var response = await _httpClient.PostAsync("checkout_sessions", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("PayMongo API Error ({StatusCode}): {Response}", response.StatusCode, responseString);
                    return null;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<PayMongoCheckoutResponse>(responseString, options);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PayMongo Checkout Session.");
                return null;
            }
        }

        public async Task<PayMongoCheckoutResponse?> GetCheckoutSessionAsync(string sessionId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"checkout_sessions/{sessionId}");
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("PayMongo API Error on Get ({StatusCode}): {Response}", response.StatusCode, responseString);
                    return null;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<PayMongoCheckoutResponse>(responseString, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve PayMongo Checkout Session {SessionId}.", sessionId);
                return null;
            }
        }
    }

    public class PayMongoCheckoutResponse
    {
        [JsonPropertyName("data")]
        public PayMongoCheckoutData? Data { get; set; }
    }

    public class PayMongoCheckoutData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public PayMongoCheckoutAttributes? Attributes { get; set; }
    }

    public class PayMongoCheckoutAttributes
    {
        [JsonPropertyName("checkout_url")]
        public string CheckoutUrl { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("payment_intent")]
        public PayMongoPaymentIntent? PaymentIntent { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class PayMongoPaymentIntent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public PayMongoPaymentIntentAttributes? Attributes { get; set; }
    }

    public class PayMongoPaymentIntentAttributes
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
