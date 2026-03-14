using System;

namespace Game.Core.Models
{
    // User-facing deck preference that biases new runs toward certain card role counts.
    public sealed class DeckComposition
    {
        public const int DeckSize = 32;

        public int BruiserCount { get; }
        public int MedicateCount { get; }
        public int InvestmentCount { get; }

        // Special slots are whatever remains after the three explicit counts are assigned.
        public int SpecialCount => DeckSize - (BruiserCount + MedicateCount + InvestmentCount);

        // Validation is intentionally strict so bad preferences fail early and clearly.
        public DeckComposition(int bruiserCount, int medicateCount, int investmentCount)
        {
            if (bruiserCount < 0 || medicateCount < 0 || investmentCount < 0)
                throw new ArgumentOutOfRangeException(nameof(bruiserCount), "Deck counts must be non-negative.");

            if (bruiserCount + medicateCount + investmentCount > DeckSize)
                throw new ArgumentOutOfRangeException(nameof(bruiserCount), $"Deck counts cannot exceed {DeckSize} total cards.");

            BruiserCount = bruiserCount;
            MedicateCount = medicateCount;
            InvestmentCount = investmentCount;
        }
    }
}
