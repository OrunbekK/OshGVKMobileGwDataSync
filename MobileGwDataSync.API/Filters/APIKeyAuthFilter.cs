using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MobileGwDataSync.API.Filters
{
    /// <summary>
    /// TODO: Авторизация по API ключу для защиты endpoints
    /// - Проверка наличия заголовка X-Api-Key
    /// - Валидация ключа против списка разрешенных ключей
    /// - Опционально: rate limiting по ключу
    /// - Логирование попыток доступа
    /// </summary>
    public class ApiKeyAuthFilter : IAuthorizationFilter
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // TODO: Реализовать проверку API ключа
            // if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            // {
            //     context.Result = new UnauthorizedResult();
            //     return;
            // }

            // var apiKey = _configuration["ApiKey"];
            // if (!apiKey.Equals(extractedApiKey))
            // {
            //     context.Result = new UnauthorizedResult();
            // }
        }
    }

    /// <summary>
    /// Атрибут для применения фильтра к контроллерам или методам
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireApiKeyAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ApiKeyAuthFilter>();
        }
    }
}