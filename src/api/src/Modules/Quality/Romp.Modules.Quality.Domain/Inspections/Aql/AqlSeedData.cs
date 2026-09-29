namespace Romp.Modules.Quality.Domain.Inspections.Aql;

/// <summary>
/// The AQL reference data (ISO 2859-1 / ANSI Z1.4): Table I (lot size to code letter, general levels I, II and III) and the
/// Table 2-A accept/reject numbers for single sampling, normal inspection, at AQL 0.65 to 6.5. Kept as data that the
/// first QC migration seeds, so an error is a data fix, never a code change (spec AC-13). Special levels S-1..S-4 have no
/// rows and are seeded inactive. <b>Verify these values against the published standard before production use</b>
/// (implementation notes): the free sources available while building were image-only or inconsistent.
/// </summary>
public static class AqlSeedData
{
    // The code letters skip I and O; the sample sizes are the ISO series 2, 3, 5, 8, 13, 20, 32, 50, 80, 125, 200, 315, 500, 800, 1250, 2000.
    private static readonly string[] Letters = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "Q", "R"];
    private static readonly int[] SampleSizes = [2, 3, 5, 8, 13, 20, 32, 50, 80, 125, 200, 315, 500, 800, 1250, 2000];

    // Table I: the 15 lot-size ranges and, per general level, the index into Letters.
    private static readonly (int Min, int Max)[] LotRanges =
    [
        (1, 8), (9, 15), (16, 25), (26, 50), (51, 90), (91, 150), (151, 280), (281, 500), (501, 1200), (1201, 3200),
        (3201, 10000), (10001, 35000), (35001, 150000), (150001, 500000), (500001, int.MaxValue),
    ];

    private static readonly Dictionary<short, int[]> LetterIndexByLevel = new()
    {
        [InspectionLevel.I] = [0, 0, 1, 2, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        [InspectionLevel.II] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14],
        [InspectionLevel.III] = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
    };

    // Table 2-A: an AQL level's first defined plan is 0/1 at this code letter; each step down the letters follows the accept-number
    // ladder, and after the ladder ends the last plan applies (the standard's up arrow).
    private static readonly int[] AcceptLadder = [0, 1, 2, 3, 5, 7, 10, 14, 21];

    private static readonly (short AqlLevelId, int FirstLetterIndex)[] AqlStarts =
    [
        (AqlLevel.Aql065, 7), // H
        (AqlLevel.Aql100, 6), // G
        (AqlLevel.Aql150, 5), // F
        (AqlLevel.Aql250, 4), // E
        (AqlLevel.Aql400, 3), // D
        (AqlLevel.Aql650, 2), // C
    ];

    /// <summary>The QC_AQL_SMPL rows: ids run 101.. for level I, 201.. for level II, 301.. for level III.</summary>
    public static IReadOnlyList<AqlSampleRow> SampleRows()
    {
        var rows = new List<AqlSampleRow>();
        foreach (var (levelId, letterIndexes) in LetterIndexByLevel)
        {
            var levelBase = (levelId - InspectionLevel.I + 1) * 100;
            for (var i = 0; i < LotRanges.Length; i++)
            {
                var letterIndex = letterIndexes[i];
                rows.Add(new AqlSampleRow((short)(levelBase + i + 1), levelId, LotRanges[i].Min, LotRanges[i].Max, Letters[letterIndex], SampleSizes[letterIndex]));
            }
        }

        return rows;
    }

    /// <summary>The QC_AQL_LIMT rows: ids run (AQL level id x 100) + step.</summary>
    public static IReadOnlyList<AqlLimitRow> LimitRows()
    {
        var rows = new List<AqlLimitRow>();
        foreach (var (aqlLevelId, first) in AqlStarts)
        {
            for (var step = 0; step < AcceptLadder.Length; step++)
            {
                rows.Add(new AqlLimitRow((short)(aqlLevelId * 100 + step + 1), Letters[first + step], aqlLevelId, AcceptLadder[step], AcceptLadder[step] + 1));
            }
        }

        return rows;
    }
}
