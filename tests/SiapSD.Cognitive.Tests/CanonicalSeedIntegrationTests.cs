using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Application;
using SiapSD.Cognitive.Domain;
using SiapSD.Cognitive.Infrastructure;

namespace SiapSD.Cognitive.Tests;

public sealed class CanonicalSeedIntegrationTests
{
    [Fact]
    public async Task Canonical_seed_persists_required_minimums_and_valid_content()
    {
        await using var fixture = await SeededDatabase.Create();
        var db = fixture.Db;
        foreach (var name in Required15) Assert.True(await PublishedCount(db, name) >= 15, name);
        Assert.True(await PublishedCount(db, "Symbol Matching") >= 40);
        var published = await db.Questions.Include(x => x.Options).Where(x => x.IsPublished).ToListAsync();
        var validator = new QuestionContentValidator();
        Assert.All(published, q => Assert.Empty(validator.Validate(q)));
        Assert.Equal(published.Count, published.Select(x => x.Code).Distinct().Count());
        Assert.Equal(published.Count, published.Select(QuestionContentValidator.Fingerprint).Distinct().Count());
        Assert.All(published, q => Assert.InRange(q.Difficulty, 1, 5));
        Assert.All(published, q => Assert.True(q.AgeMinMonths <= q.AgeMaxMonths));
        Assert.All(published.Where(x => x.QuestionType == QuestionType.GridPlanning), q => Assert.True(ValidatePlanning(q).Reachable));
    }

    [Fact]
    public async Task Canonical_seed_is_idempotent_and_does_not_republish_legacy_items()
    {
        await using var fixture = await SeededDatabase.Create();
        var db = fixture.Db;
        var before = (await db.Questions.Include(x => x.Options).ToListAsync());
        var count = before.Count; var options = before.Sum(x => x.Options.Count); var codes = before.Select(x => x.Code).Order().ToArray();
        await CanonicalQuestionBankSeedData.EnsureAsync(db);
        var after = await db.Questions.Include(x => x.Options).ToListAsync();
        Assert.Equal(count, after.Count); Assert.Equal(options, after.Sum(x => x.Options.Count)); Assert.Equal(codes, after.Select(x => x.Code).Order());
        Assert.DoesNotContain(after, q => q.Code is "CORE-S16" or "CORE-S17" or "CORE-S24" or "CORE-S26" && q.IsPublished);
    }

    static readonly string[] Required15 = ["Pattern Completion", "Missing Part", "Odd One Out", "Number Comparison", "Visual Addition", "Visual Sequence Memory", "Spatial Memory", "Object Sequence", "Word Sequence", "Instruction Sequence", "Target Detection", "Visual Search", "Inhibitory Control", "Rule Switching", "Grid Planning"];
    static async Task<int> PublishedCount(SiapSDDbContext db, string name) { var subtest = await db.Subtests.SingleAsync(s => s.Name == name); return await db.Questions.CountAsync(q => q.IsPublished && q.SubtestId == subtest.Id); }
    static PlanningPathResult ValidatePlanning(Question question) { using var json = JsonDocument.Parse(question.StimulusJson!); var r = json.RootElement; return PlanningPathValidator.Validate(r.GetProperty("rows").GetInt32(), r.GetProperty("columns").GetInt32(), r.GetProperty("start").EnumerateArray().Select(x => x.GetInt32()).ToArray(), r.GetProperty("goal").EnumerateArray().Select(x => x.GetInt32()).ToArray(), r.GetProperty("obstacles").EnumerateArray().Select(x => x.EnumerateArray().Select(y => y.GetInt32()).ToArray()), r.GetProperty("optimalSteps").GetInt32()); }

    sealed class SeededDatabase : IAsyncDisposable
    {
        readonly SqliteConnection connection; public SiapSDDbContext Db { get; }
        SeededDatabase(SqliteConnection connection, SiapSDDbContext db) { this.connection = connection; Db = db; }
        public static async Task<SeededDatabase> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SiapSDDbContext(new DbContextOptionsBuilder<SiapSDDbContext>().UseSqlite(connection).Options);
            await SeedData.EnsureAsync(db); await CanonicalQuestionBankSeedData.EnsureAsync(db);
            return new SeededDatabase(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
