using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    public class MetricsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
