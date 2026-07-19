using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.Controllers;
using NathanPortfolio.Models;
using Xunit;

namespace NathanPortfolio.Tests
{
    public class TicTacToeControllerTests
    {
        private static readonly TicTacToeController Controller = new();

        private static int GetBestMove(string?[] board, string aiMark, string humanMark)
        {
            var method = typeof(TicTacToeController).GetMethod("GetBestMove", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (int)method.Invoke(null, [board, aiMark, humanMark])!;
        }

        private static string? GetWinner(string?[] board)
        {
            var method = typeof(TicTacToeController).GetMethod("GetWinner", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string?)method.Invoke(null, [board]);
        }

        private static int GetIndex(object? value)
        {
            var property = value!.GetType().GetProperty("index")!;
            return (int)property.GetValue(value)!;
        }

        [Fact]
        public void Move_EmptyBoard_ReturnsIndexWithinRange()
        {
            var result = Controller.Move(new TicTacToeMoveRequest { Board = new string?[9], AiMark = "O" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.InRange(GetIndex(ok.Value), 0, 8);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Z")]
        public void Move_InvalidAiMark_ReturnsBadRequest(string? aiMark)
        {
            var result = Controller.Move(new TicTacToeMoveRequest { Board = new string?[9], AiMark = aiMark! });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public void Move_BoardAlreadyFull_ReturnsBadRequest()
        {
            var fullBoard = Enumerable.Repeat("X", 9).ToArray<string?>();

            var result = Controller.Move(new TicTacToeMoveRequest { Board = fullBoard, AiMark = "O" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public void GetBestMove_WinningMoveAvailable_TakesIt()
        {
            // O O _ / X X _ / _ _ _  -> O completes the top row at index 2.
            var board = new string?[] { "O", "O", null, "X", "X", null, null, null, null };

            Assert.Equal(2, GetBestMove(board, "O", "X"));
        }

        [Fact]
        public void GetBestMove_OpponentThreatensWin_BlocksIt()
        {
            // X X _ / O _ _ / _ _ _  -> O must block at index 2.
            var board = new string?[] { "X", "X", null, "O", null, null, null, null, null };

            Assert.Equal(2, GetBestMove(board, "O", "X"));
        }

        [Fact]
        public void GetBestMove_NeverLosesAcrossTheFullGameTree_WhenAiMovesFirst()
        {
            Assert.False(CanOpponentWin(new string?[9], "O", "X", aiTurn: true));
        }

        [Fact]
        public void GetBestMove_NeverLosesAcrossTheFullGameTree_WhenAiMovesSecond()
        {
            Assert.False(CanOpponentWin(new string?[9], "O", "X", aiTurn: false));
        }

        /// <summary>
        /// Exhaustively plays out every possible sequence of opponent replies against the
        /// AI's minimax move and reports whether the opponent can force a win anywhere in
        /// the tree. A perfect tic-tac-toe player can never be beaten.
        /// </summary>
        private static bool CanOpponentWin(string?[] board, string aiMark, string humanMark, bool aiTurn)
        {
            var winner = GetWinner(board);
            if (winner == humanMark) return true;
            if (winner == aiMark) return false;
            if (Array.TrueForAll(board, cell => cell is not null)) return false;

            if (aiTurn)
            {
                var move = GetBestMove(board, aiMark, humanMark);
                board[move] = aiMark;
                var lost = CanOpponentWin(board, aiMark, humanMark, aiTurn: false);
                board[move] = null;
                return lost;
            }

            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] is not null) continue;

                board[i] = humanMark;
                var lost = CanOpponentWin(board, aiMark, humanMark, aiTurn: true);
                board[i] = null;

                if (lost) return true;
            }

            return false;
        }

        [Fact]
        public void Move_OverManyCalls_DeliberatelyMistakesAboutOnePercentOfTheTime()
        {
            // Exactly one best move (index 2 completes the win), so any other index is a mistake.
            var board = new string?[] { "O", "O", null, "X", "X", null, null, null, null };
            const int trials = 5000;
            var mistakes = 0;

            for (var i = 0; i < trials; i++)
            {
                var result = Controller.Move(new TicTacToeMoveRequest { Board = (string?[])board.Clone(), AiMark = "O" });
                var ok = Assert.IsType<OkObjectResult>(result);
                if (GetIndex(ok.Value) != 2)
                    mistakes++;
            }

            // 1% target over 5000 trials (mean 50, stddev ~7); allow generous slack (0.3%-2%)
            // to avoid a flaky test while still catching the mistake path being broken, never
            // firing, or set to the wrong rate.
            var rate = mistakes / (double)trials;
            Assert.InRange(rate, 0.003, 0.02);
        }
    }
}
