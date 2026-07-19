namespace NathanPortfolio.Models
{
    /// <summary>Payload sent from the board UI whenever it needs the AI's next move.</summary>
    public class TicTacToeMoveRequest
    {
        /// <summary>9 cells, each "X", "O", or null for empty.</summary>
        public string?[] Board { get; set; } = new string?[9];

        /// <summary>Which mark the AI is playing as.</summary>
        public string AiMark { get; set; } = "O";
    }
}
