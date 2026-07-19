using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;

namespace NathanPortfolio.Controllers
{
    public class TicTacToeController(IOpenRouterService openRouter, ILogger<TicTacToeController> logger) : Controller
    {
        // How often the AI is instructed to play a deliberately suboptimal move.
        private const double MistakeChance = 0.01;

        private readonly IOpenRouterService _openRouter = openRouter;
        private readonly ILogger<TicTacToeController> _logger = logger;

        // ── GET /TicTacToe ──────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ── POST /TicTacToe/Move ────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Move([FromBody] TicTacToeMoveRequest? request)
        {
            if (request?.Board is not { Length: 9 } board ||
                (request.AiMark != "X" && request.AiMark != "O") ||
                board.Any(mark => mark is not (null or "X" or "O")))
            {
                return BadRequest(new { error = "Invalid move request." });
            }

            var emptyCells = Enumerable.Range(0, board.Length).Where(i => board[i] is null).ToList();
            if (emptyCells.Count == 0)
                return BadRequest(new { error = "The board is already full." });

            var humanMark = request.AiMark == "X" ? "O" : "X";
            var makeMistake = Random.Shared.NextDouble() < MistakeChance;

            try
            {
                var reply = await _openRouter.SendMessageAsync(
                    [new ChatMessage { Role = "user", Content = BuildBoardPrompt(board) }],
                    BuildSystemPrompt(request.AiMark, humanMark, makeMistake));

                return Ok(new { index = ParseMoveIndex(reply, emptyCells) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TicTacToe AI move failed - falling back to a random legal move.");
                return Ok(new { index = emptyCells[Random.Shared.Next(emptyCells.Count)] });
            }
        }

        // ── Prompt building ──────────────────────────────────────────────────────

        private static string BuildBoardPrompt(string?[] board)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Cells are numbered 0-8, left-to-right then top-to-bottom. Current board:");

            for (var row = 0; row < 3; row++)
            {
                var cells = Enumerable.Range(row * 3, 3).Select(i => board[i] ?? i.ToString());
                sb.AppendLine(string.Join(" | ", cells));
            }

            return sb.ToString();
        }

        private static string BuildSystemPrompt(string aiMark, string humanMark, bool makeMistake)
        {
            var strategy = makeMistake
                ? "For this move only, deliberately play a bit below your best - skip the objectively " +
                  "optimal move and pick a reasonable-looking but suboptimal one instead. It must still " +
                  "land on an empty cell; don't pick an obviously nonsensical move."
                : "Play the objectively best move: take a win if one is available, block the human's " +
                  "winning move if they threaten one, and otherwise follow optimal tic-tac-toe strategy.";

            return $"""
                You are the AI opponent in a tic-tac-toe game embedded on my portfolio site. You play as
                "{aiMark}"; the human plays as "{humanMark}". {strategy}

                Reply with ONLY the number (0-8) of the empty cell you choose. No words, no punctuation,
                just the digit.
                """;
        }

        // ── Reply parsing ────────────────────────────────────────────────────────

        private int ParseMoveIndex(string reply, List<int> emptyCells)
        {
            var match = Regex.Match(reply, @"\b[0-8]\b");
            if (match.Success && int.TryParse(match.Value, out var move) && emptyCells.Contains(move))
                return move;

            _logger.LogWarning("TicTacToe AI reply {Reply} wasn't a usable move - falling back to random.", reply);
            return emptyCells[Random.Shared.Next(emptyCells.Count)];
        }
    }
}
