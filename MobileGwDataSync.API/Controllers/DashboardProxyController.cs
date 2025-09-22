using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashboardProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardProxyController> _logger;

        public DashboardProxyController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DashboardProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Прокси для вызова защищенных API методов
        /// </summary>
        [HttpGet("{*path}")]
        [HttpPost("{*path}")]
        [HttpPut("{*path}")]
        [HttpDelete("{*path}")]
        public async Task<IActionResult> Proxy(string path)
        {
            try
            {
                // Получаем внутренний API ключ из конфигурации
                var apiKey = _configuration["Dashboard:InternalApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Internal API key not configured");
                    return StatusCode(500, new { error = "Service configuration error" });
                }

                // Создаем HTTP клиент
                var client = _httpClientFactory.CreateClient();

                // Формируем запрос
                var request = new HttpRequestMessage
                {
                    Method = new HttpMethod(Request.Method),
                    RequestUri = new Uri($"http://localhost:8080/api/{path}{Request.QueryString}")
                };

                // Добавляем X-API-Key
                request.Headers.Add("X-API-Key", apiKey);

                // Копируем тело запроса если есть
                if (Request.ContentLength > 0)
                {
                    request.Content = new StreamContent(Request.Body);
                    request.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(Request.ContentType ?? "application/json");
                }

                // Логируем для аудита
                _logger.LogInformation(
                    "Dashboard proxy: User {User} calling {Method} {Path}",
                    User.Identity?.Name,
                    request.Method,
                    path
                );

                // Выполняем запрос
                var response = await client.SendAsync(request);

                // Возвращаем результат
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Proxy request failed for path: {Path}", path);
                return StatusCode(500, new { error = "Proxy request failed" });
            }
        }
    }
}