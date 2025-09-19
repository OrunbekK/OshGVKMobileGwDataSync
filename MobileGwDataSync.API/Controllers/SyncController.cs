using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    public class SyncController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
