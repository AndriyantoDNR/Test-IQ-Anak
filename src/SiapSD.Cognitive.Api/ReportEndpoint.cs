using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Infrastructure;

public static class ReportEndpoint
{
    public static async Task<IResult> Create(Guid id, SiapSDDbContext db)
    {
        var session = await db.AssessmentSessions.Include(x => x.Responses).FirstOrDefaultAsync(x => x.Id == id);
        if (session is null) return Results.NotFound();
        var child = await db.Children.FindAsync(session.ChildId);
        var domains = await db.CognitiveDomains.ToDictionaryAsync(x => x.Id, x => x.Name);
        var subtests = await db.Subtests.ToDictionaryAsync(x => x.Id, x => x.Name);
        var result = session.Responses.GroupBy(x => x.CognitiveDomainId).Select(domain => new
        {
            name = domains.GetValueOrDefault(domain.Key, "Area kegiatan"),
            subtests = domain.GroupBy(x => x.SubtestId).Select(subtest => new
            {
                name = subtests.GetValueOrDefault(subtest.Key, "Kegiatan"),
                metrics = subtest.Select(ToMetric).ToArray()
            }).ToArray()
        }).ToArray();
        return Results.Ok(new { child = child is null ? null : new { child.Name }, assessmentDate = session.StartedAtUtc, durationMs = session.TotalDurationMs, domains = result, disclaimer = "Catatan perkembangan sesi ini bukan diagnosis." });
    }

    static object ToMetric(SiapSD.Cognitive.Domain.AssessmentResponse response)
    {
        using var snapshot = JsonDocument.Parse(response.QuestionSnapshotJson);
        var type = snapshot.RootElement.TryGetProperty("QuestionType", out var item)
            ? item.ValueKind switch
            {
                JsonValueKind.String => item.GetString() ?? "Kegiatan",
                JsonValueKind.Number when item.TryGetInt32(out var value) => Enum.GetName(typeof(SiapSD.Cognitive.Domain.QuestionType), value) ?? "Kegiatan",
                _ => "Kegiatan"
            }
            : "Kegiatan";
        var values = new Dictionary<string, object?> { ["benar"] = response.IsCorrect, ["waktuResponsMs"] = response.ResponseTimeMs };
        if (!string.IsNullOrWhiteSpace(response.ResponseTelemetryJson))
        {
            using var telemetry = JsonDocument.Parse(response.ResponseTelemetryJson);
            foreach (var property in telemetry.RootElement.EnumerateObject()) values[property.Name] = Value(property.Value);
        }
        return new { questionType = type, metrics = values };
    }

    static object? Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var whole) => whole,
        JsonValueKind.Number => value.GetDecimal(),
        _ => null
    };
}
