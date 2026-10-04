using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Application;
using SiapSD.Cognitive.Domain;
namespace SiapSD.Cognitive.Infrastructure;
public class AssessmentService(SiapSDDbContext db, IQuestionSelectionService selector, IAdaptiveDifficultyService adaptive, IWorkingMemoryEngine workingMemory) : IAssessmentService {
 public async Task<AssessmentSession> StartAssessment(Guid childId, Guid blueprintId) { var child=await db.Children.FindAsync(childId)??throw new KeyNotFoundException("Child not found."); var bp=await db.AssessmentBlueprints.FindAsync(blueprintId)??throw new KeyNotFoundException("Blueprint not found."); var s=new AssessmentSession{ChildId=childId,AssessmentBlueprintId=bp.Id,Status=AssessmentStatus.InProgress,StartedAtUtc=DateTime.UtcNow,AgeInMonthsAtAssessment=child.AgeInMonths()}; db.AssessmentSessions.Add(s); await db.SaveChangesAsync(); return s; }
 public Task<AssessmentSession?> GetAssessment(Guid id)=>db.AssessmentSessions.Include(x=>x.Responses).FirstOrDefaultAsync(x=>x.Id==id);
 public async Task<Question?> GetNextQuestion(Guid id) {
  var session = await GetAssessment(id) ?? throw new KeyNotFoundException("Assessment not found.");
  if (session.Status != AssessmentStatus.InProgress) return null;

  var blueprintItems = await db.AssessmentBlueprintItems
   .Where(item => item.AssessmentBlueprintId == session.AssessmentBlueprintId)
   .OrderBy(item => item.DisplayOrder)
   .ToListAsync();
  var allQuestions = await db.Questions.Include(question => question.Options).ToListAsync();

  foreach (var blueprintItem in blueprintItems) {
   // Progress is forward-only: a later item cannot make an earlier item eligible again.
   if (session.CurrentSubtestId.HasValue && blueprintItem.SubtestId != session.CurrentSubtestId && session.Responses.Any(response => response.SubtestId == blueprintItem.SubtestId)) continue;
   var answeredForItem = session.Responses.Count(response => response.SubtestId == blueprintItem.SubtestId);
   if (answeredForItem >= blueprintItem.MaximumQuestions) continue;

   var responsesForItem = session.Responses.Where(response => response.SubtestId == blueprintItem.SubtestId).OrderBy(response => response.QuestionOrder).ToArray();
   var memoryItem = allQuestions.Any(question => question.SubtestId == blueprintItem.SubtestId && IsWorkingMemory(question));
   var difficulty = memoryItem
    ? WorkingMemoryDifficulty(session, responsesForItem)
    : adaptive.NextDifficulty(blueprintItem.InitialDifficulty, responsesForItem.Select(response => response.IsCorrect));
   if (difficulty is null) continue;
   var question = selector.Select(allQuestions, session, blueprintItem, difficulty.Value);
   if (question is null) continue;

   session.CurrentSubtestId = blueprintItem.SubtestId;
   await db.SaveChangesAsync();
   return question;
  }

  await CompleteAssessment(id);
  return null;
 }
 public async Task<AssessmentResponse> SubmitAnswer(Guid id, Guid qid, string answer, long responseTimeMs, bool firstInteraction) {
  if(responseTimeMs < 0 || responseTimeMs > 7_200_000) throw new ArgumentOutOfRangeException(nameof(responseTimeMs));
  var s=await GetAssessment(id)??throw new KeyNotFoundException("Assessment not found."); if(s.Status!=AssessmentStatus.InProgress)throw new InvalidOperationException("Assessment is not active."); if(await db.AssessmentResponses.AnyAsync(x=>x.AssessmentSessionId==id&&x.QuestionId==qid))throw new InvalidOperationException("Question already submitted.");
  var q=await db.Questions.Include(x=>x.Options).FirstOrDefaultAsync(x=>x.Id==qid)??throw new KeyNotFoundException("Question not found."); var memory=IsWorkingMemory(q); var attention=IsAttention(q); int planningUndo=0,planningReset=0,planningRun=0,planningWrong=0; long planningTime=responseTimeMs;
  if(q.QuestionType==QuestionType.GridPlanning){try{using var p=System.Text.Json.JsonDocument.Parse(answer);if(p.RootElement.ValueKind==System.Text.Json.JsonValueKind.Object){var commands=p.RootElement.GetProperty("commands").EnumerateArray().Select(x=>x.GetString()??throw new ArgumentException("Invalid planning command.")).ToArray();if(commands.Any(x=>x is not ("Up" or "Down" or "Left" or "Right")))throw new ArgumentException("Invalid planning command.");planningUndo=p.RootElement.GetProperty("undoCount").GetInt32();planningReset=p.RootElement.GetProperty("resetCount").GetInt32();planningRun=p.RootElement.GetProperty("runCount").GetInt32();planningWrong=p.RootElement.GetProperty("wrongSteps").GetInt32();planningTime=p.RootElement.GetProperty("planningTimeMs").GetInt64();if(planningUndo<0||planningReset<0||planningRun<0||planningWrong<0||planningTime<0)throw new ArgumentException("Invalid planning telemetry.");answer=System.Text.Json.JsonSerializer.Serialize(commands);}}catch(System.Text.Json.JsonException){throw new ArgumentException("Invalid planning response.",nameof(answer));}}
  string[] expected=[]; string[] actual=[]; WorkingMemoryTrialResult? evaluated=null;
  if(memory) { try { expected=System.Text.Json.JsonSerializer.Deserialize<string[]>(q.CorrectAnswerJson)??throw new ArgumentException("Invalid memory question answer."); actual=System.Text.Json.JsonSerializer.Deserialize<string[]>(answer)??throw new ArgumentException("Memory response must be a sequence."); } catch(System.Text.Json.JsonException) { throw new ArgumentException("Memory response must be a JSON string sequence.",nameof(answer)); } evaluated=new WorkingMemoryEngine().Evaluate(expected,actual); }
  else if(attention) ValidateAttention(q,answer);
  else if(q.QuestionType!=QuestionType.GridPlanning && (answer.TrimStart().StartsWith("{")||answer.TrimStart().StartsWith("["))) throw new ArgumentException("Standard response must be a scalar value.",nameof(answer));
  var correct=evaluated?.FullyCorrect ?? (attention ? AttentionCorrect(q,answer) : q.QuestionType==QuestionType.GridPlanning ? q.CorrectAnswerJson==answer : (q.Options.Any(x=>x.IsCorrect && (x.Code==answer||x.Value==answer)) || q.CorrectAnswerJson.Trim('"','[',']')==answer));
  // Each seeded executive/speed item is one server-scored trial. Aggregate reports sum these stable fields.
  var telemetry=memory ? System.Text.Json.JsonSerializer.Serialize(new { sequenceLength=expected.Length, presentationDurationMs=ReadInt(q.StimulusJson,"presentationDurationMs",1000), retentionGapMs=ReadInt(q.StimulusJson,"retentionGapMs",1000), correctPositions=evaluated!.CorrectPositions, totalPositions=expected.Length, positionAccuracy=expected.Length==0?0m:(decimal)evaluated.CorrectPositions/expected.Length, fullyCorrect=evaluated.FullyCorrect, errorTypes=evaluated.ErrorType, modality=q.QuestionType.ToString() }) : attention ? AttentionTelemetry(q,answer,responseTimeMs) : q.QuestionType==QuestionType.GridPlanning ? System.Text.Json.JsonSerializer.Serialize(new { optimalSteps=4,stepsProgrammed=System.Text.Json.JsonSerializer.Deserialize<string[]>(answer)?.Length??0,executedSteps=System.Text.Json.JsonSerializer.Deserialize<string[]>(answer)?.Length??0,wrongSteps=planningWrong,undoCount=planningUndo,resetCount=planningReset,runCount=planningRun,planningTimeMs=planningTime,solved=correct }) : q.QuestionType==QuestionType.InhibitoryControl ? System.Text.Json.JsonSerializer.Serialize(new { trialsAttempted=1,correctResponses=correct?1:0,incorrectResponses=correct?0:1,accuracy=correct?100:0,inhibitionErrors=correct?0:1,medianResponseTimeMs=responseTimeMs }) : q.QuestionType==QuestionType.RuleSwitch ? System.Text.Json.JsonSerializer.Serialize(new { trialsAttempted=1,correctResponses=correct?1:0,incorrectResponses=correct?0:1,accuracy=correct?100:0,ruleSwitchCount=1,switchErrors=correct?0:1,perseverationErrors=correct?0:1,switchLatencyMs=responseTimeMs,medianResponseTimeMs=responseTimeMs }) : q.QuestionType==QuestionType.SymbolMatching ? System.Text.Json.JsonSerializer.Serialize(new { attempted=1,correct=correct?1:0,incorrect=correct?0:1,accuracy=correct?100:0,correctPerMinute=correct?1:0,medianResponseTimeMs=responseTimeMs,durationMs=60000 }) : null;
  var now=DateTime.UtcNow; var r=new AssessmentResponse{AssessmentSessionId=id,QuestionId=qid,QuestionVersion=q.Version,QuestionSnapshotJson=System.Text.Json.JsonSerializer.Serialize(new {q.Code,q.QuestionType,q.Instruction,q.QuestionText,q.StimulusJson}),CognitiveDomainId=q.CognitiveDomainId,SubtestId=q.SubtestId,Difficulty=q.Difficulty,PresentedAtUtc=now,FirstInteractionAtUtc=firstInteraction?now:null,AnsweredAtUtc=now,ResponseTimeMs=responseTimeMs,FinalAnswerJson=memory?answer:$"\"{answer}\"",IsCorrect=correct,QuestionOrder=s.CurrentQuestionIndex++,SessionElapsedMs=(long)(now-(s.StartedAtUtc??now)).TotalMilliseconds,ResponseTelemetryJson=telemetry}; db.AssessmentResponses.Add(r); await db.SaveChangesAsync(); return r;
 }
 static int ReadInt(string? json,string name,int fallback) { try { using var d=System.Text.Json.JsonDocument.Parse(json??"{}"); return d.RootElement.TryGetProperty(name,out var p)&&p.TryGetInt32(out var n)?n:fallback; } catch { return fallback; } }
 static bool IsWorkingMemory(Question question) => question.QuestionType is QuestionType.MemorySequence or QuestionType.SpatialMemory or QuestionType.AuditorySequence or QuestionType.InstructionSequence;
 static bool IsAttention(Question question) => question.QuestionType is QuestionType.TargetDetection or QuestionType.VisualSearch;
 static void ValidateAttention(Question question,string answer) { try { using var task=System.Text.Json.JsonDocument.Parse(question.StimulusJson??"{}"); using var response=System.Text.Json.JsonDocument.Parse(answer); if(response.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Array) throw new ArgumentException("Attention response must be an array.",nameof(answer)); var valid=task.RootElement.GetProperty("items").EnumerateArray().Select((_,i)=>i.ToString()).ToHashSet(); var selected=response.RootElement.EnumerateArray().Select(x=>x.GetString()??throw new ArgumentException("Attention selection must be a string.",nameof(answer))).ToArray(); if(selected.Length!=selected.Distinct().Count()||selected.Any(x=>!valid.Contains(x))) throw new ArgumentException("Attention response contains invalid selections.",nameof(answer)); } catch(System.Text.Json.JsonException) { throw new ArgumentException("Attention response must be valid JSON.",nameof(answer)); } }
 static bool AttentionCorrect(Question question,string answer) { try { using var d=System.Text.Json.JsonDocument.Parse(answer); var selected=d.RootElement.EnumerateArray().Select(x=>x.GetString()??"").OrderBy(x=>x).ToArray(); var expected=System.Text.Json.JsonSerializer.Deserialize<string[]>(question.CorrectAnswerJson)!.OrderBy(x=>x).ToArray(); return selected.SequenceEqual(expected); } catch { return false; } }
 static string AttentionTelemetry(Question question,string answer,long responseTimeMs) { try { using var d=System.Text.Json.JsonDocument.Parse(answer); var selected=d.RootElement.EnumerateArray().Select(x=>x.GetString()??"").ToHashSet(); var expected=System.Text.Json.JsonSerializer.Deserialize<string[]>(question.CorrectAnswerJson)!.ToHashSet(); var hits=selected.Count(expected.Contains); var missed=expected.Count-hits; var falsePositives=selected.Count- hits; return System.Text.Json.JsonSerializer.Serialize(new { modality=question.QuestionType.ToString(), targetCount=expected.Count, correctHits=hits, correctSelections=hits, missedTargets=missed, falsePositives, falseSelections=falsePositives, accuracy=expected.Count==0?0m:(decimal)hits/expected.Count, completionTimeMs=responseTimeMs }); } catch { return System.Text.Json.JsonSerializer.Serialize(new { modality=question.QuestionType.ToString(), targetCount=0, correctHits=0, missedTargets=0, falsePositives=0, accuracy=0m, completionTimeMs=responseTimeMs }); } }
 int? WorkingMemoryDifficulty(AssessmentSession session, IReadOnlyList<AssessmentResponse> responses) {
  var progress = new WorkingMemoryProgress(workingMemory.InitialSpan(session.AgeInMonthsAtAssessment), 0, 0, false);
  foreach (var response in responses) progress = workingMemory.Next(progress, response.IsCorrect);
  return progress.Stop ? null : progress.Span;
 }
 public async Task PauseAssessment(Guid id){var s=await GetAssessment(id)??throw new KeyNotFoundException();s.Status=AssessmentStatus.Paused;await db.SaveChangesAsync();} public async Task ResumeAssessment(Guid id){var s=await GetAssessment(id)??throw new KeyNotFoundException();s.Status=AssessmentStatus.InProgress;await db.SaveChangesAsync();} public async Task CompleteAssessment(Guid id){var s=await GetAssessment(id)??throw new KeyNotFoundException();if(s.Status==AssessmentStatus.Completed)return;s.Status=AssessmentStatus.Completed;s.CompletedAtUtc=DateTime.UtcNow;s.TotalDurationMs=(long)(s.CompletedAtUtc.Value-(s.StartedAtUtc??s.CompletedAtUtc.Value)).TotalMilliseconds;var scorer=new BasicSubtestScorer();foreach(var g in s.Responses.GroupBy(x=>x.CognitiveDomainId)){var memoryResponses=g.Where(r=>IsWorkingMemoryResponse(r)).ToArray();var score=memoryResponses.Length==g.Count()?WorkingMemoryScore(memoryResponses):scorer.Score(g);db.AssessmentDomainScores.Add(new AssessmentDomainScore{AssessmentSessionId=id,CognitiveDomainId=g.Key,RawScore=score.RawScore,InternalScore=score.InternalScore,MetricsJson=score.MetricsJson});}await db.SaveChangesAsync();}
 static bool IsWorkingMemoryResponse(AssessmentResponse response) => response.ResponseTelemetryJson is not null;
 ScoreResult WorkingMemoryScore(IEnumerable<AssessmentResponse> responses) { var metrics=workingMemory.Metrics(responses); return new(metrics.FullyCorrectTrials, Math.Round(100m*metrics.HighestStableSpan/7m,2), System.Text.Json.JsonSerializer.Serialize(metrics)); }
}
