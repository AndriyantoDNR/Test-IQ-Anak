using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Domain;
namespace SiapSD.Cognitive.Infrastructure;

public static class WorkingMemorySeedData {
 public static async Task EnsureAsync(SiapSDDbContext db) {
  var wm=await db.CognitiveDomains.SingleAsync(x=>x.Name=="Working Memory");
  var audio=await db.CognitiveDomains.SingleAsync(x=>x.Name=="Auditory Memory");
  var tasks=new[]{("Visual Sequence Memory",wm.Id,QuestionType.MemorySequence,new[]{"red","blue","green","yellow"}), ("Spatial Memory",wm.Id,QuestionType.SpatialMemory,new[]{"0,0","1,1","2,0","0,2"}), ("Object Sequence",wm.Id,QuestionType.MemorySequence,new[]{"ball","book","star","car"}), ("Word Sequence",audio.Id,QuestionType.AuditorySequence,new[]{"rumah","bola","buku","kucing"}), ("Instruction Sequence",audio.Id,QuestionType.InstructionSequence,new[]{"tepuk","lompat","duduk","angkat tangan"})};
  foreach(var (name,domainId,type,sequence) in tasks) {
   var subtest=await db.Subtests.SingleAsync(x=>x.Name==name); var blueprint=await db.AssessmentBlueprints.SingleAsync(x=>x.Code=="STANDARD-5-6"); var item=await db.AssessmentBlueprintItems.SingleOrDefaultAsync(x=>x.AssessmentBlueprintId==blueprint.Id&&x.SubtestId==subtest.Id); if(item is null) { item=new AssessmentBlueprintItem{AssessmentBlueprintId=blueprint.Id,CognitiveDomainId=domainId,SubtestId=subtest.Id,InitialDifficulty=2,MinimumQuestions=1,MaximumQuestions=12,DisplayOrder=100+tasks.ToList().FindIndex(x=>x.Item1==name)}; db.AssessmentBlueprintItems.Add(item); } else item.MaximumQuestions=12;
   var legacy=(await db.Questions.Where(x=>x.SubtestId==subtest.Id&&x.Code.StartsWith($"DEV-WM-{subtest.Code}-")).ToListAsync()).Where(x=>x.Code.Count(c=>c=='-')==3); foreach(var question in legacy) question.IsPublished=false;
   for(var span=2;span<=7;span++) for(var trial=1;trial<=2;trial++){var code=$"DEV-WM-{subtest.Code}-SPAN-{span}-{trial}";if(await db.Questions.AnyAsync(x=>x.Code==code))continue;var seq=Enumerable.Range(0,span).Select(i=>sequence[(i+trial-1)%sequence.Length]).ToArray();db.Questions.Add(new Question{Code=code,CognitiveDomainId=domainId,SubtestId=subtest.Id,QuestionType=type,Difficulty=span,AgeMinMonths=48,AgeMaxMonths=120,Instruction="Perhatikan urutan, lalu ulangi setelah layar kosong.",QuestionText=name,StimulusJson=System.Text.Json.JsonSerializer.Serialize(new{sequence=seq,presentationDurationMs=900,retentionGapMs=900}),CorrectAnswerJson=System.Text.Json.JsonSerializer.Serialize(seq),IsPublished=true});}
  }
  var legacyScalarAnswers=await db.Questions.Where(x=>x.CorrectAnswerJson=="\"B\""||x.CorrectAnswerJson=="B").ToListAsync(); foreach(var question in legacyScalarAnswers) question.CorrectAnswerJson="[\"B\"]";
  await db.SaveChangesAsync();
 }
}
