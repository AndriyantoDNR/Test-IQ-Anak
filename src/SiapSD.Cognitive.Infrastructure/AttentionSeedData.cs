using Microsoft.EntityFrameworkCore;
using SiapSD.Cognitive.Domain;
namespace SiapSD.Cognitive.Infrastructure;

public static class AttentionSeedData {
 public static async Task EnsureAsync(SiapSDDbContext db) {
  var domain=await db.CognitiveDomains.SingleAsync(x=>x.Name=="Attention"); var blueprint=await db.AssessmentBlueprints.SingleAsync(x=>x.Code=="STANDARD-5-6");
  var tasks=new[]{("Target Detection",QuestionType.TargetDetection,"⭐",new[]{"0","3","6"},new[]{"⭐","●","▲","⭐","■","●","⭐","▲","■"}), ("Visual Search",QuestionType.VisualSearch,"🍎",new[]{"1","4","8"},new[]{"●","🍎","▲","■","🍎","●","▲","■","🍎"})};
  foreach(var (name,type,target,correct,items) in tasks) { var subtest=await db.Subtests.SingleAsync(x=>x.Name==name); if(!await db.AssessmentBlueprintItems.AnyAsync(x=>x.AssessmentBlueprintId==blueprint.Id&&x.SubtestId==subtest.Id)) db.AssessmentBlueprintItems.Add(new AssessmentBlueprintItem{AssessmentBlueprintId=blueprint.Id,CognitiveDomainId=domain.Id,SubtestId=subtest.Id,InitialDifficulty=1,MinimumQuestions=1,MaximumQuestions=1,DisplayOrder=200+(type==QuestionType.VisualSearch?1:0)}); var code=$"DEV-ATT-{subtest.Code}-1"; if(!await db.Questions.AnyAsync(x=>x.Code==code)) db.Questions.Add(new Question{Code=code,CognitiveDomainId=domain.Id,SubtestId=subtest.Id,QuestionType=type,Difficulty=1,AgeMinMonths=48,AgeMaxMonths=120,Instruction=$"Pilih semua gambar {target}.",QuestionText=name,StimulusJson=System.Text.Json.JsonSerializer.Serialize(new {target,items}),CorrectAnswerJson=System.Text.Json.JsonSerializer.Serialize(correct),IsPublished=true}); }
  await db.SaveChangesAsync();
 }
}
