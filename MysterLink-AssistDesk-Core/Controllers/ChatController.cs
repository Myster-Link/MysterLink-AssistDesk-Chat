using Microsoft.AspNetCore.Mvc;

namespace MysterLink_AssistDesk_Core.Controllers
{
    public class ChatController : Controller
    {
        public IActionResult Index(string user)
        {
            if (string.IsNullOrEmpty(user))
                return RedirectToAction("Index", "Home");

            ViewBag.User = user;
            return View();
        }
    }
}