using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;

namespace NathanPortfolio.Controllers
{
    public class ContactController(IEmailSender emailSender, IConfiguration configuration) : Controller
    {
        private readonly IEmailSender _emailSender = emailSender;
        private readonly IConfiguration _configuration = configuration;

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(ContactUserMessage contactUserMessage)
        {
            try
            {
                await _emailSender.SendEmailAsync(contactUserMessage.FirstName, contactUserMessage.LastName, contactUserMessage.Email, contactUserMessage.Message, _configuration);

                ViewBag.ResponseMessage = "Email Sent Sucessfully";
            }

            catch
            {
                ViewBag.ResponseMessage = "Email Sent Unsuccessfully";
            }

            string? baseUrl = Url.Action("Index", "Contact", null, Request.Scheme);

            return Redirect(baseUrl + $"?ResponseMessage={ViewBag.ResponseMessage}");
        }
    }
}
