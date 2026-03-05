using Xunit;

namespace Aspire.Hosting.Minecraft.Tests;

/// <summary>
/// Tests for parsing .squad/team.md to extract active agent names.
/// The squad detection feature reads team.md, parses the Members markdown table,
/// and returns agent names — excluding system agents (Scribe, Ralph).
///
/// These tests are written proactively from requirements. Once Shuri implements
/// the SquadAgentParser (or equivalent), wire these tests to the real parser.
/// Until then, they validate the parsing logic contract via a local test helper.
/// </summary>
public class SquadAgentNameParsingTests
{
    // ====================================================================
    // PARSING CONTRACT (mirrors expected implementation)
    // ====================================================================

    /// <summary>
    /// Names of system agents that should always be excluded from results.
    /// Scribe is a session logger, Ralph is a work monitor — neither are "real" agents.
    /// </summary>
    private static readonly HashSet<string> ExcludedAgents = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scribe",
        "Ralph"
    };

    /// <summary>
    /// Parses a team.md file content and extracts active agent names,
    /// excluding system agents (Scribe, Ralph).
    /// This is a test-local reference implementation matching the expected contract.
    /// </summary>
    private static List<string> ParseSquadAgentNames(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var lines = content.Split('\n', StringSplitOptions.None);
        var inMembersSection = false;
        var headerFound = false;
        var nameColumnIndex = -1;
        var results = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Look for "## Members" section header
            if (line.StartsWith("## Members", StringComparison.OrdinalIgnoreCase))
            {
                inMembersSection = true;
                continue;
            }

            // Stop at next section header
            if (inMembersSection && line.StartsWith("## ") && !line.StartsWith("## Members", StringComparison.OrdinalIgnoreCase))
                break;

            if (!inMembersSection) continue;

            // Parse table header row to find "Name" column
            if (line.StartsWith('|') && !headerFound && line.Contains("Name", StringComparison.OrdinalIgnoreCase))
            {
                var columns = line.Split('|', StringSplitOptions.None)
                    .Select(c => c.Trim())
                    .ToArray();
                for (var i = 0; i < columns.Length; i++)
                {
                    if (columns[i].Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        nameColumnIndex = i;
                        break;
                    }
                }
                headerFound = true;
                continue;
            }

            // Skip separator row (|---|---|...)
            if (headerFound && line.StartsWith('|') && line.Contains("---"))
                continue;

            // Parse data rows
            if (headerFound && nameColumnIndex >= 0 && line.StartsWith('|'))
            {
                var columns = line.Split('|', StringSplitOptions.None);
                if (nameColumnIndex < columns.Length)
                {
                    var name = columns[nameColumnIndex].Trim();
                    if (!string.IsNullOrEmpty(name) && !ExcludedAgents.Contains(name))
                    {
                        results.Add(name);
                    }
                }
            }
        }

        return results;
    }

    // ====================================================================
    // VALID INPUT TESTS
    // ====================================================================

    [Fact]
    public void ParseTeamMd_ValidTable_ExtractsActiveAgentNames()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |
            | Shuri | Backend Dev | `.squad/agents/shuri/charter.md` | ✅ Active |
            | Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
            | Ralph | Work Monitor | — | 🔄 Monitor |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Equal(2, names.Count);
        Assert.Contains("Rhodey", names);
        Assert.Contains("Shuri", names);
    }

    [Fact]
    public void ParseTeamMd_ValidTable_ExcludesScribe()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
            | Nebula | Tester | `.squad/agents/nebula/charter.md` | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Single(names);
        Assert.Equal("Nebula", names[0]);
        Assert.DoesNotContain("Scribe", names);
    }

    [Fact]
    public void ParseTeamMd_ValidTable_ExcludesRalph()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Ralph | Work Monitor | — | 🔄 Monitor |
            | Rocket | Builder | `.squad/agents/rocket/charter.md` | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Single(names);
        Assert.Equal("Rocket", names[0]);
        Assert.DoesNotContain("Ralph", names);
    }

    [Fact]
    public void ParseTeamMd_ValidTable_PreservesOrderFromTable()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Wong | DevOps | `.squad/agents/wong/charter.md` | ✅ Active |
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |
            | Nebula | Tester | `.squad/agents/nebula/charter.md` | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Equal(3, names.Count);
        Assert.Equal("Wong", names[0]);
        Assert.Equal("Rhodey", names[1]);
        Assert.Equal("Nebula", names[2]);
    }

    [Fact]
    public void ParseTeamMd_AllSystemAgents_ReturnsEmptyList()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
            | Ralph | Work Monitor | — | 🔄 Monitor |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_LargeTeam_ExtractsAllNonSystemAgents()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |
            | Shuri | Backend Dev | `.squad/agents/shuri/charter.md` | ✅ Active |
            | Rocket | Builder | `.squad/agents/rocket/charter.md` | ✅ Active |
            | Nebula | Tester | `.squad/agents/nebula/charter.md` | ✅ Active |
            | Wong | DevOps | `.squad/agents/wong/charter.md` | ✅ Active |
            | Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |
            | Ralph | Work Monitor | — | 🔄 Monitor |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Equal(5, names.Count);
        Assert.Contains("Rhodey", names);
        Assert.Contains("Shuri", names);
        Assert.Contains("Rocket", names);
        Assert.Contains("Nebula", names);
        Assert.Contains("Wong", names);
    }

    // ====================================================================
    // MISSING / EMPTY INPUT TESTS
    // ====================================================================

    [Fact]
    public void ParseTeamMd_NullContent_ReturnsEmptyList()
    {
        var names = ParseSquadAgentNames(null);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_EmptyString_ReturnsEmptyList()
    {
        var names = ParseSquadAgentNames("");

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_WhitespaceOnly_ReturnsEmptyList()
    {
        var names = ParseSquadAgentNames("   \n  \n  ");

        Assert.Empty(names);
    }

    // ====================================================================
    // MALFORMED INPUT TESTS
    // ====================================================================

    [Fact]
    public void ParseTeamMd_NoMembersSection_ReturnsEmptyList()
    {
        var content = """
            # My Squad

            Some general info about the squad.

            ## Configuration

            | Setting | Value |
            |---------|-------|
            | Mode | Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_MembersSectionWithNoTable_ReturnsEmptyList()
    {
        var content = """
            ## Members

            The team hasn't been set up yet.
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_TableWithoutNameColumn_ReturnsEmptyList()
    {
        var content = """
            ## Members

            | Agent | Role | Status |
            |-------|------|--------|
            | Rhodey | Lead | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_EmptyTableRows_ReturnsEmptyList()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Empty(names);
    }

    [Fact]
    public void ParseTeamMd_MembersFollowedByOtherSection_StopsAtNextSection()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |

            ## Configuration

            | Name | Value |
            |------|-------|
            | NotAnAgent | SomeValue |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Single(names);
        Assert.Equal("Rhodey", names[0]);
    }

    // ====================================================================
    // CASE SENSITIVITY TESTS
    // ====================================================================

    [Fact]
    public void ParseTeamMd_ExclusionIsCaseInsensitive()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | scribe | Session Logger | test | 📋 Silent |
            | RALPH | Work Monitor | — | 🔄 Monitor |
            | Nebula | Tester | test | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Single(names);
        Assert.Equal("Nebula", names[0]);
    }

    // ====================================================================
    // COLUMN EXTRACTION TESTS
    // ====================================================================

    [Fact]
    public void ParseTeamMd_ExtractsOnlyNamesNotOtherColumns()
    {
        var content = """
            ## Members

            | Name | Role | Charter | Status |
            |------|------|---------|--------|
            | Rhodey | Lead | `.squad/agents/rhodey/charter.md` | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Single(names);
        Assert.Equal("Rhodey", names[0]);
        // Ensure we didn't accidentally grab other columns
        Assert.DoesNotContain("Lead", names);
        Assert.DoesNotContain("Active", names);
    }

    [Fact]
    public void ParseTeamMd_NameColumnNotFirst_StillExtractsCorrectly()
    {
        var content = """
            ## Members

            | Role | Name | Charter | Status |
            |------|------|---------|--------|
            | Lead | Rhodey | `.squad/agents/rhodey/charter.md` | ✅ Active |
            | Tester | Nebula | `.squad/agents/nebula/charter.md` | ✅ Active |
            """;

        var names = ParseSquadAgentNames(content);

        Assert.Equal(2, names.Count);
        Assert.Contains("Rhodey", names);
        Assert.Contains("Nebula", names);
    }
}
