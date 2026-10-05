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
            if (!old.Code.StartsWith("CORE-", StringComparison.Ordinal) || !int.TryParse(old.Code.Split('-').Last(), out _)) old.IsPublished = false;

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
            if (item is null) db.AssessmentBlueprintItems.Add(new AssessmentBlueprintItem { AssessmentBlueprintId = blueprint.Id, CognitiveDomainId = subtest.CognitiveDomainId, SubtestId = subtest.Id, InitialDifficulty = 1, MinimumQuestions = 1, MaximumQuestions = 1, DisplayOrder = order + subtest.DisplayOrder });
            else item.MaximumQuestions = 1;
            for (var index = 1; index <= count; index++)
            {
                var v1Code = $"CORE-{subtest.Code}-{index:000}";
                if (family == "standard")
                {
                    // Retire template V1 rows rather than rewriting historic content.
                    var v1 = await db.Questions.SingleOrDefaultAsync(x => x.Code == v1Code);
                    if (v1 is not null) v1.IsPublished = false;
                }
                // Semantic-display V3 is a new canonical row; historic rows remain available
                // to existing response snapshots but cannot be selected again.
                foreach (var previous in await db.Questions.Where(x => x.Code.StartsWith(v1Code)).ToListAsync()) previous.IsPublished = false;
                var code = $"{v1Code}-V3";
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
            q.StimulusJson = JsonSerializer.Serialize(new { sequence, candidatePool = pool, presentationDurationMs = 700 + difficulty * 100, retentionGapMs = 700 + difficulty * 100, span }); q.CorrectAnswerJson = JsonSerializer.Serialize(sequence);
        }
        else if (family == "planning")
        {
            q.QuestionType = QuestionType.GridPlanning; var obstacle = new[] { 1 + (index - 1) / 4, 1 + (index - 1) % 4 };
            q.Instruction = "Susun langkah agar robot mencapai bintang."; q.QuestionText = "Bantu robot menuju bintang.";
            q.StimulusJson = JsonSerializer.Serialize(new { rows = 6, columns = 6, start = new[] { 0, 0 }, goal = new[] { 5, 5 }, obstacles = new[] { obstacle }, optimalSteps = 10 }); q.CorrectAnswerJson = "[\"Right\",\"Right\",\"Right\",\"Right\",\"Right\",\"Down\",\"Down\",\"Down\",\"Down\",\"Down\"]";
        }
        else
        {
            q.QuestionType = family switch { "attention" => subtest.Name == "Target Detection" ? QuestionType.TargetDetection : QuestionType.VisualSearch, "executive" => subtest.Name == "Inhibitory Control" ? QuestionType.InhibitoryControl : QuestionType.RuleSwitch, "speed" => QuestionType.SymbolMatching, "standard" when subtest.Name == "Pattern Completion" => QuestionType.PatternChoice, "standard" when subtest.Name == "Missing Part" => QuestionType.MissingPart, "standard" when subtest.Name == "Odd One Out" => QuestionType.OddOneOut, _ => QuestionType.MathChoice };
            if (family == "standard") { PopulateStandard(q, subtest.Name, index, difficulty); return q; }
            if (family == "attention")
            {
                var target = subtest.Name == "Target Detection" ? "⭐" : "🔺";
                var displays = subtest.Name == "Target Detection" ? new[] { "⭐", "●", "▲", "■", "⭐", "●", "▲", "■", "⭐" } : new[] { "🔻", "🔺", "🔶", "🔺", "🔻", "🔶", "🔺", "🔻", "🔶" };
                var items = displays.Select((display, position) => new { id = $"cell-{index}-{position}", display }).ToArray();
                q.Instruction = "Sentuh semua gambar yang sama dengan contoh."; q.QuestionText = "Cari gambar yang cocok";
                q.StimulusJson = JsonSerializer.Serialize(new { variant = index, target = new { id = $"target-{index}", display = target }, items }); q.CorrectAnswerJson = JsonSerializer.Serialize(items.Where(x => x.display == target).Select(x => x.id));
            }
            else if (family == "executive")
            {
                var ruleSwitch = subtest.Name == "Rule Switching";
                q.Instruction = ruleSwitch ? "ATURAN SEKARANG: pilih berdasarkan WARNA." : "Pilih warna yang BERLAWANAN dengan kartu.";
                q.QuestionText = ruleSwitch ? "Kartu ini berwarna biru. Pilih BIRU." : "Kartu ini berwarna merah. Pilih BIRU.";
                q.StimulusJson = JsonSerializer.Serialize(new { variant = index, rule = ruleSwitch ? "warna" : "lawan-warna", stimulus = new { display = ruleSwitch ? "🔵 ▲" : "🔴" }, responseDisplays = new[] { "🔴 Merah", "🔵 Biru", "🟡 Kuning" } }); q.CorrectAnswerJson = JsonSerializer.Serialize("B");
                q.Options.AddRange(new[] { new QuestionOption { Code = "A", Text = "🔴 Merah", DisplayOrder = 1 }, new QuestionOption { Code = "B", Text = "🔵 Biru", DisplayOrder = 2, IsCorrect = true }, new QuestionOption { Code = "C", Text = "🟡 Kuning", DisplayOrder = 3 } });
            }
            else
            {
                q.Instruction = "Pilih gambar yang sama dengan contoh."; q.QuestionText = "Contoh: 🐟 — mana pasangan yang sama?";
                q.StimulusJson = JsonSerializer.Serialize(new { variant = index, target = new { id = $"symbol-{index}", display = "🐟" }, durationMs = 60000 }); q.CorrectAnswerJson = JsonSerializer.Serialize("B");
                q.Options.AddRange(new[] { new QuestionOption { Code = "A", Text = "🌟", DisplayOrder = 1 }, new QuestionOption { Code = "B", Text = "🐟", DisplayOrder = 2, IsCorrect = true }, new QuestionOption { Code = "C", Text = "🍎", DisplayOrder = 3 } });
            }
        }
        return q;
    }

    static void PopulateStandard(Question q, string family, int index, int difficulty)
    {
        var (prompt, correct, first, third) = family switch
        {
            "Pattern Completion" => Pattern(index),
            "Missing Part" => MissingPart(index),
            "Odd One Out" => OddOneOut(index),
            "Number Comparison" => NumberComparison(index),
            "Visual Addition" => VisualAddition(index),
            _ => throw new InvalidOperationException($"Unsupported standard family: {family}")
        };
        q.Instruction = family == "Number Comparison" ? "Lihat kedua kelompok, lalu pilih jawaban yang benar." : family == "Visual Addition" ? "Hitung gambar-gambar ini, lalu pilih jumlahnya." : "Perhatikan baik-baik, lalu pilih jawaban yang paling tepat.";
        q.QuestionText = prompt;
        q.StimulusJson = JsonSerializer.Serialize(new { task = family, prompt, difficulty, meaningful = true });
        q.CorrectAnswerJson = JsonSerializer.Serialize("B");
        q.Options.AddRange(new[] { new QuestionOption { Code = "A", Text = first, DisplayOrder = 1 }, new QuestionOption { Code = "B", Text = correct, DisplayOrder = 2, IsCorrect = true }, new QuestionOption { Code = "C", Text = third, DisplayOrder = 3 } });
    }

    static (string, string, string, string) Pattern(int i)
    {
        var patterns = new[] { ("○ △ ○ △ ○ ?", "△", "○", "□"), ("★ ★ ● ★ ★ ● ★ ★ ?", "●", "★", "▲"), ("→ ↑ → ↑ → ?", "↑", "→", "↓"), ("kecil ○, sedang ○, besar ○, kecil ○, sedang ○, ?", "besar ○", "kecil ○", "sedang ○"), ("merah ★, biru ●, merah ★, biru ●, ?", "merah ★", "biru ★", "merah ●") };
        var p = patterns[(i - 1) % patterns.Length]; return ($"Lanjutkan pola: {p.Item1}", p.Item2, p.Item3, p.Item4);
    }
    static (string, string, string, string) MissingPart(int i)
    {
        var items = new[] { ("Sebuah rumah memiliki dinding, pintu, jendela, dan bagian yang hilang agar lengkap adalah …", "atap", "roda", "sirip"), ("Wajah memiliki dua mata, hidung, dan mulut. Bagian yang hilang adalah …", "mata", "roda", "ekor"), ("Sepeda memiliki roda, setang, dan pedal. Bagian yang hilang agar dapat dikendarai adalah …", "roda", "sayap", "kelopak"), ("Ikan memiliki kepala, badan, ekor, dan bagian yang hilang adalah …", "sirip", "pintu", "ban"), ("Bunga memiliki kelopak, batang, dan bagian hijau di samping batang. Bagian itu adalah …", "daun", "roda", "jendela") };
        var x = items[(i - 1) % items.Length]; return x;
    }
    static (string, string, string, string) OddOneOut(int i)
    {
        var items = new[] { ("🐱  🐶  🐰  🚗\nMana yang berbeda?", "🚗", "🐱", "🐶"), ("🍎  🍌  🍇  ⚽\nMana yang berbeda?", "⚽", "🍎", "🍌"), ("□  △  ○  🐟\nMana yang berbeda?", "🐟", "□", "△"), ("🚌  🚗  🚲  🍞\nMana yang berbeda?", "🍞", "🚌", "🚗"), ("🌹  🌻  🌷  🐦\nMana yang berbeda?", "🐦", "🌹", "🌻") };
        return items[(i - 1) % items.Length];
    }
    static (string, string, string, string) NumberComparison(int i)
    {
        var left = 2 + i % 5; var right = left + (i % 3 == 0 ? 0 : 1);
        var correct = right > left ? "kelompok kanan" : "sama banyak";
        return ($"Mana yang lebih banyak?\nKiri: {new string('●', left)}\nKanan: {new string('●', right)}", correct, "kelompok kiri", right > left ? "sama banyak" : "kelompok kanan");
    }
    static (string, string, string, string) VisualAddition(int i)
    {
        var left = 1 + i % 4; var right = 1 + (i / 3) % 4; var sum = left + right;
        var applesLeft = string.Concat(Enumerable.Repeat("apel", left)); var applesRight = string.Concat(Enumerable.Repeat("apel", right));
        return ($"{applesLeft} + {applesRight} = ?", sum.ToString(), (sum - 1).ToString(), (sum + 1).ToString()); /*
        return ($"{string.Concat(Enumerable.Repeat(\"🍎\", left))} + {string.Concat(Enumerable.Repeat(\"🍎\", right))} = ?", sum.ToString(), (sum - 1).ToString(), (sum + 1).ToString());
    */ }
}
