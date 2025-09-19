using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    public class AlertsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
