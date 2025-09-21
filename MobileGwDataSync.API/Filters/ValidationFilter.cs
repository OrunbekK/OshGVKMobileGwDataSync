using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MobileGwDataSync.API.Filters
{
    /// <summary>
    /// TODO: Автоматическая валидация входных моделей
    /// - Проверка ModelState.IsValid
    /// - Форматирование ошибок валидации в единый формат
    /// - Возврат 400 Bad Request с деталями ошибок
    /// </summary>
    public class ValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // TODO: Реализовать проверку ModelState
            // if (!context.ModelState.IsValid)
            // {
            //     var errors = context.ModelState
            //         .Where(x => x.Value.Errors.Count > 0)
            //         .ToDictionary(
            //             kvp => kvp.Key,
            //             kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
            //         );
            //     
            //     context.Result = new BadRequestObjectResult(new { errors });
            // }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Не требуется
        }
    }
}