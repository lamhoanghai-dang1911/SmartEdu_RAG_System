using Microsoft.AspNetCore.Mvc;

namespace SmartEdu.Web.Controllers
{
    public class RBLController : Controller
    {
        // Trang Dashboard chính
        public IActionResult Index()
        {
            return View();
        }
    }
}
