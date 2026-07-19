using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.Models;

namespace NathanPortfolio.Controllers
{
    public class TicTacToeController : Controller
    {
        // How often the AI deliberately plays a suboptimal move instead of its best one.
        private const double MistakeChance = 0.01;

        private static readonly int[][] WinLines =
        [
            [0, 1, 2], [3, 4, 5], [6, 7, 8],
            [0, 3, 6], [1, 4, 7], [2, 5, 8],
            [0, 4, 8], [2, 4, 6]
        ];

        // ── GET /TicTacToe ──────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ── POST /TicTacToe/Move ────────────────────────────────────────────────

        [HttpPost]
        public IActionResult Move([FromBody] TicTacToeMoveRequest? request)
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
            var bestMove = GetBestMove(board, request.AiMark, humanMark);

            var makeMistake = Random.Shared.NextDouble() < MistakeChance;
            var index = makeMistake ? PickSuboptimalMove(emptyCells, bestMove) : bestMove;

            return Ok(new { index });
        }

        // ── Move selection ──────────────────────────────────────────────────────

        /// <summary>
        /// Picks a legal cell other than the optimal move, for the rare deliberate mistake.
        /// Falls back to the optimal move if it's the only legal cell left.
        /// </summary>
        private static int PickSuboptimalMove(List<int> emptyCells, int bestMove)
        {
            var alternatives = emptyCells.Where(i => i != bestMove).ToList();
            return alternatives.Count == 0 ? bestMove : alternatives[Random.Shared.Next(alternatives.Count)];
        }

        /// <summary>
        /// Returns the index of the game-theoretically optimal move for <paramref name="aiMark"/>
        /// via exhaustive minimax search. Tic-tac-toe's state space is tiny (at most 9! states),
        /// so this always runs instantly and always plays perfectly.
        /// </summary>
        private static int GetBestMove(string?[] board, string aiMark, string humanMark)
        {
            var bestScore = int.MinValue;
            var bestMove = -1;

            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] is not null) continue;

                board[i] = aiMark;
                var score = Minimax(board, depth: 1, isMaximizing: false, aiMark, humanMark);
                board[i] = null;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = i;
                }
            }

            return bestMove;
        }

        private static int Minimax(string?[] board, int depth, bool isMaximizing, string aiMark, string humanMark)
        {
            var winner = GetWinner(board);
            if (winner == aiMark) return 10 - depth;
            if (winner == humanMark) return depth - 10;
            if (Array.TrueForAll(board, mark => mark is not null)) return 0;

            var turnMark = isMaximizing ? aiMark : humanMark;
            var best = isMaximizing ? int.MinValue : int.MaxValue;

            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] is not null) continue;

                board[i] = turnMark;
                var score = Minimax(board, depth + 1, !isMaximizing, aiMark, humanMark);
                board[i] = null;

                best = isMaximizing ? Math.Max(best, score) : Math.Min(best, score);
            }

            return best;
        }

        private static string? GetWinner(string?[] board)
        {
            foreach (var line in WinLines)
            {
                var (a, b, c) = (line[0], line[1], line[2]);
                if (board[a] is not null && board[a] == board[b] && board[b] == board[c])
                    return board[a];
            }

            return null;
        }
    }
}
