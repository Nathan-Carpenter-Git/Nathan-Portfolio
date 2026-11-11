using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using NathanPortfolio.Controllers;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;

namespace NathanPortfolio.Tests
{
    public class ContactControllerTests
    {
        private static readonly ContactUserMessage Message = new()
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Message = "Hello",
        };

        private static ContactController CreateController(Mock<IEmailSender> emailSender, bool captchaPasses, string? token = "token")
        {
            var validator = new Mock<ITurnstileValidator>();
            validator
                .Setup(v => v.ValidateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(captchaPasses);

            var httpContext = new DefaultHttpContext();
            var form = new Dictionary<string, StringValues>();
            if (token is not null)
                form["cf-turnstile-response"] = token;
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(form);

            return new ContactController(emailSender.Object, validator.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
            };
        }

        private static string? ResponseMessage(IActionResult result)
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ContactController.Index), redirect.ActionName);
            return redirect.RouteValues?["ResponseMessage"] as string;
        }

        [Fact]
        public async Task Index_Post_CaptchaFails_DoesNotSendEmail()
        {
            var emailSender = new Mock<IEmailSender>();

            var result = await CreateController(emailSender, captchaPasses: false).Index(Message);

            Assert.Equal(ContactController.CaptchaFailedMessage, ResponseMessage(result));
            emailSender.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Index_Post_CaptchaPasses_SendsEmail()
        {
            var emailSender = new Mock<IEmailSender>();

            var result = await CreateController(emailSender, captchaPasses: true).Index(Message);

            Assert.Equal(ContactController.SentMessage, ResponseMessage(result));
            emailSender.Verify(e => e.SendEmailAsync("Jane", "Doe", "jane@example.com", "Hello"), Times.Once);
        }

        [Fact]
        public async Task Index_Post_EmailThrows_ReportsFailure()
        {
            var emailSender = new Mock<IEmailSender>();
            emailSender
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("smtp down"));

            var result = await CreateController(emailSender, captchaPasses: true).Index(Message);

            Assert.Equal(ContactController.FailedMessage, ResponseMessage(result));
        }
    }
}
