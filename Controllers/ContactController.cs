using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;

namespace NathanPortfolio.Controllers
{
    public class ContactController(IEmailSender emailSender, ITurnstileValidator turnstileValidator) : Controller
    {
        public const string SentMessage = "Email Sent Successfully";
        public const string FailedMessage = "Email Sent Unsuccessfully";
        public const string CaptchaFailedMessage = "Captcha Verification Failed";

        // Form field the Turnstile widget adds to the form it sits in.
        private const string TurnstileField = "cf-turnstile-response";

        private readonly IEmailSender _emailSender = emailSender;
        private readonly ITurnstileValidator _turnstileValidator = turnstileValidator;

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(ContactUserMessage contactUserMessage)
        {
            string responseMessage;

            string? token = Request.HasFormContentType ? Request.Form[TurnstileField].ToString() : null;

            if (!await _turnstileValidator.ValidateAsync(token, HttpContext.RequestAborted))
            {
                responseMessage = CaptchaFailedMessage;
            }
            else
            {
                try
                {
                    await _emailSender.SendEmailAsync(contactUserMessage.FirstName, contactUserMessage.LastName, contactUserMessage.Email, contactUserMessage.Message);

                    responseMessage = SentMessage;
                }
                catch
                {
                    responseMessage = FailedMessage;
                }
            }

            return RedirectToAction(nameof(Index), new { ResponseMessage = responseMessage });
        }
    }
}
