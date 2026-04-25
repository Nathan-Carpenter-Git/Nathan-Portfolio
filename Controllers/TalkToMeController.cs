using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;
using System.Text.Json;

namespace NathanPortfolio.Controllers
{
    public class TalkToMeController : Controller
    {
        private const string DefaultSystemContext =
            "You are a helpful, concise assistant on Nathan Carpenter's personal portfolio website. " +
            "Be friendly and professional.";

        private readonly IOpenRouterService _openRouter;
        private readonly ILogger<TalkToMeController> _logger;

        public TalkToMeController(
            IOpenRouterService openRouter,
            ILogger<TalkToMeController> logger)
        {
            _openRouter = openRouter;
            _logger     = logger;
        }

        // ── GET /TalkToMe ──────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            // Pass the current system context to the view so it pre-fills the editor.
            ViewData["SystemContext"] = DefaultSystemContext;
            return View();
        }

        // ── POST /TalkToMe/SendMessage ─────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Message cannot be empty." });

            // Rebuild the full history from what the client sent, then append
            // the new user message so the service sees the complete conversation.
            var history = (request.History ?? new List<ChatMessage>())
                .Where(m => m.Role is "user" or "assistant"
                         && !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            history.Add(new ChatMessage
            {
                Role    = "user",
                Content = request.Message.Trim()
            });

            var context = string.IsNullOrWhiteSpace(request.SystemContext)
                ? DefaultSystemContext
                : request.SystemContext.Trim();

            try
            {
                var reply = await _openRouter.SendMessageAsync(history, context);
                return Ok(new { reply });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OpenRouter API request failed.");
                return StatusCode(502, new
                {
                    error = "The AI service is unavailable. Please try again shortly."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TalkToMe/SendMessage.");
                return StatusCode(500, new
                {
                    error = "An unexpected error occurred."
                });
            }
        }
    }

    // ── Request DTO ────────────────────────────────────────────────────────────

    /// <summary>
    /// Payload sent from the chat UI on every message.
    /// </summary>
    public class SendMessageRequest
    {
        /// <summary>The new message the user just typed.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// All previous turns (user + assistant) so the model has full context.
        /// The client maintains this list and sends it with every request.
        /// </summary>
        public List<ChatMessage>? History { get; set; }

        /// <summary>The editable system prompt from the context panel.</summary>
        public string? SystemContext { get; set; }
    }
}
