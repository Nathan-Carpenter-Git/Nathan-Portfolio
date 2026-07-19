using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NathanPortfolio.Controllers;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;
using Xunit;

namespace NathanPortfolio.Tests
{
    public class TicTacToeControllerTests
    {
        private static TicTacToeController CreateController(Mock<IOpenRouterService> openRouterMock) =>
            new(openRouterMock.Object, Mock.Of<ILogger<TicTacToeController>>());

        private static string? CapturedSystemContext(Mock<IOpenRouterService> mock)
        {
            var invocation = mock.Invocations.LastOrDefault(i => i.Method.Name == nameof(IOpenRouterService.SendMessageAsync));
            return invocation?.Arguments[1] as string;
        }

        [Fact]
        public async Task Move_AiRepliesWithUsableDigit_ReturnsThatIndex()
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            openRouterMock.Setup(s => s.SendMessageAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>()))
                          .ReturnsAsync("4");
            var controller = CreateController(openRouterMock);

            var result = await controller.Move(new TicTacToeMoveRequest
            {
                Board = new string?[9],
                AiMark = "O"
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(4, GetIndex(ok.Value));
        }

        [Fact]
        public async Task Move_AiRepliesWithUnusableText_FallsBackToARandomLegalCell()
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            openRouterMock.Setup(s => s.SendMessageAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>()))
                          .ReturnsAsync("I decline to play.");
            var controller = CreateController(openRouterMock);

            var board = new string?[9];
            board[4] = "X"; // only the other 8 cells are legal

            var result = await controller.Move(new TicTacToeMoveRequest { Board = board, AiMark = "O" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var index = GetIndex(ok.Value);
            Assert.NotEqual(4, index);
            Assert.InRange(index, 0, 8);
        }

        [Fact]
        public async Task Move_AiServiceThrows_FallsBackToARandomLegalCellInsteadOfFailing()
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            openRouterMock.Setup(s => s.SendMessageAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>()))
                          .ThrowsAsync(new HttpRequestException("OpenRouter unavailable"));
            var controller = CreateController(openRouterMock);

            var result = await controller.Move(new TicTacToeMoveRequest
            {
                Board = new string?[9],
                AiMark = "X"
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.InRange(GetIndex(ok.Value), 0, 8);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Z")]
        public async Task Move_InvalidAiMark_ReturnsBadRequest(string? aiMark)
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            var controller = CreateController(openRouterMock);

            var result = await controller.Move(new TicTacToeMoveRequest { Board = new string?[9], AiMark = aiMark! });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Move_BoardAlreadyFull_ReturnsBadRequest()
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            var controller = CreateController(openRouterMock);
            var fullBoard = Enumerable.Repeat("X", 9).ToArray<string?>();

            var result = await controller.Move(new TicTacToeMoveRequest { Board = fullBoard, AiMark = "O" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Move_SystemPromptSentToAi_NeverContainsTheNathanChatPersona()
        {
            // The chat widget's Nathan persona must stay scoped to TalkToMe; it must never
            // leak into the TicTacToe AI's system prompt, which plays an unrelated game role.
            var openRouterMock = new Mock<IOpenRouterService>();
            openRouterMock.Setup(s => s.SendMessageAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>()))
                          .ReturnsAsync("0");
            var controller = CreateController(openRouterMock);

            await controller.Move(new TicTacToeMoveRequest { Board = new string?[9], AiMark = "O" });

            var systemContext = CapturedSystemContext(openRouterMock);
            Assert.NotNull(systemContext);
            Assert.DoesNotContain("Nathan Carpenter", systemContext);
            Assert.DoesNotContain("Cattaraugus County Bank", systemContext);
            Assert.Contains("tic-tac-toe", systemContext, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Move_OverManyCalls_AiIsInstructedToDeliberatelyMistakeAboutOneQuarterOfTheTime()
        {
            var openRouterMock = new Mock<IOpenRouterService>();
            openRouterMock.Setup(s => s.SendMessageAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<string>()))
                          .ReturnsAsync("0");
            var controller = CreateController(openRouterMock);

            const int trials = 400;
            var mistakeInstructions = 0;

            for (var i = 0; i < trials; i++)
            {
                await controller.Move(new TicTacToeMoveRequest { Board = new string?[9], AiMark = "O" });
                var systemContext = CapturedSystemContext(openRouterMock)!;
                if (systemContext.Contains("deliberately play a bit below your best", StringComparison.OrdinalIgnoreCase))
                    mistakeInstructions++;
            }

            // 25% target over 400 trials; allow generous slack (12%-38%) to avoid a flaky test
            // while still catching the mistake-instruction path being broken or never firing.
            var rate = mistakeInstructions / (double)trials;
            Assert.InRange(rate, 0.12, 0.38);
        }

        private static int GetIndex(object? value)
        {
            var property = value!.GetType().GetProperty("index")!;
            return (int)property.GetValue(value)!;
        }
    }
}
