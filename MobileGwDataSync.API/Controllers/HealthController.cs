using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    public class HealthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
