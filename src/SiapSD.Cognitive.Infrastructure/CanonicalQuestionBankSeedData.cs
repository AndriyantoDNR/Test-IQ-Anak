using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Application;
using SiapSD.Cognitive.Domain;

namespace SiapSD.Cognitive.Infrastructure;

/// <summary>Idempotent, validator-gated canonical content bank. Legacy items remain for historical snapshots but are unpublished.</summary>
public static class CanonicalQuestionBankSeedData
{
    public static async Task EnsureAsync(SiapSDDbContext db)
    {
        var blueprint = await db.AssessmentBlueprints.SingleAsync(x => x.Code == "STANDARD-5-6");
        var subtests = await db.Subtests.ToDictionaryAsync(x => x.Name);
        var validator = new QuestionContentValidator();
        foreach (var old in await db.Questions.Where(x => x.IsPublished).ToListAsync())
            if (!old.Code.StartsWith("CORE-", StringComparison.Ordinal) || old.Code.Split('-').Last().Length != 3) old.IsPublished = false;

        var standard = new[] { "Pattern Completion", "Missing Part", "Odd One Out", "Number Comparison", "Visual Addition" };
        foreach (var name in standard) await AddFamily(name, 15, "standard", 0);
        foreach (var name in new[] { "Visual Sequence Memory", "Spatial Memory", "Object Sequence", "Word Sequence", "Instruction Sequence" }) await AddFamily(name, 15, "memory", 100);
        foreach (var name in new[] { "Target Detection", "Visual Search" }) await AddFamily(name, 15, "attention", 200);
        foreach (var name in new[] { "Inhibitory Control", "Rule Switching" }) await AddFamily(name, 15, "executive", 300);
        await AddFamily("Grid Planning", 15, "planning", 400);
        await AddFamily("Symbol Matching", 40, "speed", 500);
        await db.SaveChangesAsync();

        async Task AddFamily(string name, int count, string family, int order)
        {
            var subtest = subtests[name];
            var item = await db.AssessmentBlueprintItems.SingleOrDefaultAsync(x => x.AssessmentBlueprintId == blueprint.Id && x.SubtestId == subtest.Id);
            if (item is null) db.AssessmentBlueprintItems.Add(new AssessmentBlueprintItem { AssessmentBlueprintId = blueprint.Id, CognitiveDomainId = subtest.CognitiveDomainId, SubtestId = subtest.Id, InitialDifficulty = 1, MinimumQuestions = 1, MaximumQuestions = count, DisplayOrder = order + subtest.DisplayOrder });
            else item.MaximumQuestions = count;
            for (var index = 1; index <= count; index++)
            {
                var code = $"CORE-{subtest.Code}-{index:000}";
                if (await db.Questions.AnyAsync(x => x.Code == code)) continue;
                var question = Create(subtest, code, index, family);
                var errors = validator.Validate(question);
                if (errors.Count > 0) throw new InvalidOperationException("Invalid canonical seed: " + string.Join("; ", errors));
                db.Questions.Add(question);
            }
        }
    }

    static Question Create(Subtest subtest, string code, int index, string family)
    {
        var difficulty = Math.Min(5, 1 + (index - 1) / 3);
        var q = new Question { Code = code, CognitiveDomainId = subtest.CognitiveDomainId, SubtestId = subtest.Id, Difficulty = difficulty, AgeMinMonths = 48, AgeMaxMonths = 120, IsPublished = true, IsAnswerRandomized = true };
        if (family == "memory")
        {
            q.QuestionType = subtest.Name switch { "Spatial Memory" => QuestionType.SpatialMemory, "Word Sequence" => QuestionType.AuditorySequence, "Instruction Sequence" => QuestionType.InstructionSequence, _ => QuestionType.MemorySequence };
            var span = 2 + (index - 1) / 3; // span is intentionally separate from difficulty.
            var pool = subtest.Name == "Spatial Memory" ? new[] { "0,0", "1,2", "2,1", "0,2", "2,0", "1,1" } : new[] { "bola", "buku", "rumah", "bintang", "mobil", "kucing", "tepuk" };
            var sequence = Enumerable.Range(0, span).Select(x => pool[(x + index) % pool.Length]).ToArray();
            q.Instruction = "Perhatikan urutan, lalu ulangi setelah layar kosong."; q.QuestionText = $"{subtest.Name} — urutan {index}";
            q.StimulusJson = JsonSerializer.Serialize(new { sequence, presentationDurationMs = 700 + difficulty * 100, retentionGapMs = 700 + difficulty * 100, span }); q.CorrectAnswerJson = JsonSerializer.Serialize(sequence);
        }
        else if (family == "planning")
        {
            q.QuestionType = QuestionType.GridPlanning; var obstacle = new[] { 1 + (index - 1) / 4, 1 + (index - 1) % 4 };
            q.Instruction = "Susun langkah agar anak mencapai bintang."; q.QuestionText = $"Rute grid {index}";
            q.StimulusJson = JsonSerializer.Serialize(new { rows = 6, columns = 6, start = new[] { 0, 0 }, goal = new[] { 5, 5 }, obstacles = new[] { obstacle }, optimalSteps = 10 }); q.CorrectAnswerJson = "[\"Right\",\"Right\",\"Right\",\"Right\",\"Right\",\"Down\",\"Down\",\"Down\",\"Down\",\"Down\"]";
        }
        else
        {
            q.QuestionType = family switch { "attention" => subtest.Name == "Target Detection" ? QuestionType.TargetDetection : QuestionType.VisualSearch, "executive" => subtest.Name == "Inhibitory Control" ? QuestionType.InhibitoryControl : QuestionType.RuleSwitch, "speed" => QuestionType.SymbolMatching, "standard" when subtest.Name == "Pattern Completion" => QuestionType.PatternChoice, "standard" when subtest.Name == "Missing Part" => QuestionType.MissingPart, "standard" when subtest.Name == "Odd One Out" => QuestionType.OddOneOut, _ => QuestionType.MathChoice };
            var correct = "B"; q.Instruction = family == "attention" ? "Pilih semua target yang diminta." : family == "executive" ? "Ikuti aturan pada kartu." : "Pilih jawaban terbaik."; q.QuestionText = $"{subtest.Name} — latihan {index}";
            q.StimulusJson = JsonSerializer.Serialize(new { task = subtest.Name, item = index, target = $"target-{index}", items = new[] { $"distractor-{index}", $"target-{index}", $"distractor-{index + 1}" }, durationMs = family == "speed" ? 60000 : 0 }); q.CorrectAnswerJson = JsonSerializer.Serialize(correct);
            q.Options.AddRange(new[] { new QuestionOption { Code = "A", Text = $"Pilihan {index}A", DisplayOrder = 1 }, new QuestionOption { Code = "B", Text = $"Pilihan {index}B", DisplayOrder = 2, IsCorrect = true }, new QuestionOption { Code = "C", Text = $"Pilihan {index}C", DisplayOrder = 3 } });
        }
        return q;
    }
}
