using Microsoft.AspNetCore.Mvc;

namespace MobileGwDataSync.API.Controllers
{
    public class JobsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
