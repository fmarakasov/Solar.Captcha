using Solar.Captcha.SampleApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Solar.Captcha.SampleApp.Controllers;

public class HomeController(ISessionCaptcha captcha) : Controller
{
    public IActionResult Index()
    {
        return View(new SessionCaptchaModel());
    }

    [HttpPost]
    public IActionResult Index(SessionCaptchaModel model)
    {
        if (ModelState.IsValid)
        {
            bool isValidCaptcha = captcha.Validate(model.CaptchaCode, HttpContext.Session);
            return Content(isValidCaptcha ? "Success" : "Invalid captcha code");
        }

        return BadRequest();
    }

    [Route("captcha-image-action")]
    public IActionResult CaptchaImage()
    {
        var s = captcha.GenerateCaptchaImageFileStream(HttpContext.Session);
        return s;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}