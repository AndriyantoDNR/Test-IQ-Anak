using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SiapSD.Cognitive.Domain;

namespace SiapSD.Cognitive.Application;

public sealed record PlanningPathResult(bool Reachable, int ShortestPathLength, string? Error = null);

public static class PlanningPathValidator
{
    public static PlanningPathResult Validate(int rows, int columns, int[] start, int[] goal, IEnumerable<int[]> obstacles, int optimalSteps)
    {
        if (rows < 2 || columns < 2 || start.Length != 2 || goal.Length != 2) return new(false, 0, "Invalid grid or coordinates.");
        var blocked = obstacles.Select(x => x.Length == 2 ? (x[0], x[1]) : (-1, -1)).ToArray();
        bool Inside((int r, int c) p) => p.r >= 0 && p.c >= 0 && p.r < rows && p.c < columns;
        var source = (start[0], start[1]); var target = (goal[0], goal[1]);
        if (!Inside(source) || !Inside(target) || source == target || blocked.Distinct().Count() != blocked.Length || blocked.Any(x => !Inside(x)) || blocked.Contains(source) || blocked.Contains(target)) return new(false, 0, "Invalid planning layout.");
        var seen = new HashSet<(int,int)> { source }; var queue = new Queue<((int,int) point, int steps)>(); queue.Enqueue((source, 0));
        while (queue.Count > 0) { var (point, steps) = queue.Dequeue(); if (point == target) return steps == optimalSteps ? new(true, steps) : new(false, steps, "optimalSteps does not match BFS."); foreach (var next in new[]{(point.Item1-1,point.Item2),(point.Item1+1,point.Item2),(point.Item1,point.Item2-1),(point.Item1,point.Item2+1)}) if (Inside(next) && !blocked.Contains(next) && seen.Add(next)) queue.Enqueue((next, steps + 1)); }
        return new(false, 0, "Goal is unreachable.");
    }
}

public sealed class QuestionContentValidator
{
    public IReadOnlyList<string> Validate(Question question)
    {
        var errors = new List<string>(); string Prefix(string text) => $"{question.Code} ({question.QuestionType}): {text}";
        if (!question.Code.StartsWith("CORE-", StringComparison.Ordinal)) errors.Add(Prefix("canonical seed code is required"));
        if (question.Difficulty is < 1 or > 5) errors.Add(Prefix("difficulty must be 1-5"));
        if (question.AgeMinMonths < 0 || question.AgeMinMonths > question.AgeMaxMonths) errors.Add(Prefix("invalid age range"));
        if (string.IsNullOrWhiteSpace(question.Instruction) || string.IsNullOrWhiteSpace(question.StimulusJson) || string.IsNullOrWhiteSpace(question.CorrectAnswerJson)) errors.Add(Prefix("required content is missing"));
        if (question.Options.GroupBy(x => x.Code).Any(x => x.Count() > 1) || question.Options.GroupBy(x => x.Text.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) errors.Add(Prefix("duplicate options"));
        if (question.Options.Count > 0 && question.Options.Count(x => x.IsCorrect) != 1) errors.Add(Prefix("exactly one correct option is required"));
        try { using var json = JsonDocument.Parse(question.StimulusJson); if (question.QuestionType == QuestionType.GridPlanning) { var root = json.RootElement; var result = PlanningPathValidator.Validate(root.GetProperty("rows").GetInt32(), root.GetProperty("columns").GetInt32(), root.GetProperty("start").EnumerateArray().Select(x=>x.GetInt32()).ToArray(), root.GetProperty("goal").EnumerateArray().Select(x=>x.GetInt32()).ToArray(), root.GetProperty("obstacles").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()), root.GetProperty("optimalSteps").GetInt32()); if (!result.Reachable) errors.Add(Prefix(result.Error!)); } } catch (Exception) { errors.Add(Prefix("malformed stimulus")); }
        return errors;
    }

    public static string Fingerprint(Question question)
    {
        var normalized = string.Join('|', question.QuestionType, question.CognitiveDomainId, question.SubtestId, Normalize(question.Instruction), NormalizeJson(question.StimulusJson), string.Join(';', question.Options.OrderBy(x=>x.Text,StringComparer.OrdinalIgnoreCase).Select(x=>Normalize(x.Text))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
    static string Normalize(string? value) => string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    static string NormalizeJson(string? value)
    {
        using var doc = JsonDocument.Parse(value ?? "{}");
        return Canonicalize(doc.RootElement);
    }

    static string Canonicalize(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',', element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => JsonSerializer.Serialize(x.Name) + ":" + Canonicalize(x.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(',', element.EnumerateArray().Select(Canonicalize)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(Normalize(element.GetString())),
        _ => element.GetRawText()
    };
}
