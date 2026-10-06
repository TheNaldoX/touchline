using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public sealed class LeadershipConversationTests
    {
        Database db;Career c;
        [SetUp] public void Setup()
        {
            db=new Database{clubs=new[]{new ClubData{id="a",name="A",annualRevenue=40000000},new ClubData{id="b",name="B",annualRevenue=30000000}},players=Enumerable.Range(0,44).Select(i=>new PlayerData{id="p"+i,name="Joueur "+i,team=i<22?"a":"b",position=i%22==0?"GK":"CM",positions=new[]{i%22==0?"GK":"CM"},rating=70,potential=82,age=i==21?20:30,wage=1000,fitness=100,morale=75}).ToArray()};c=new Career{club="a"};c.lineup=Career.Select(db,"a",c.tactic);c.EnsureLife(db);
        }
        LaunchCareerEntry ReadSaved(string json)
        {
            // Exercise the actual loader: native JsonUtility can deserialize
            // null match/world references as empty placeholder objects.
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/unity/leadership-save-unit",Guid.NewGuid().ToString("N")+".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json);
            var loaded=LaunchCareerStorage.Read(path,"Leadership validation",db);
            Assert.IsTrue(loaded.Valid,loaded.Error);Assert.IsNull(loaded.State.match);Assert.AreEqual(json,File.ReadAllText(path));return loaded;
        }
        void Talk(string id,bool phone=false)=>c.Talk(db,id,"leadership",phone);
        [Test] public void FirstCredibleLeaderStillEncouragesTheGroup(){Talk("p1");Assert.AreEqual(77,c.Person("p1").morale,.001);Assert.AreEqual(75.3,c.Person("p21").morale,.001);Assert.That(c.life.messages.Last().text,Does.Contain("montrer l’exemple"));}
        [Test] public void AskingEveryVeteranOnTheSameDayCannotMultiplyGroupMorale(){for(int i=0;i<21;i++)Talk("p"+i);Assert.AreEqual(75.3,c.Person("p21").morale,.001);Assert.AreEqual(77,c.Person("p0").morale,.001);Assert.AreEqual(75.3,c.Person("p20").morale,.001);}
        [Test] public void DuplicateMessageExplainsThatTheGroupNeedsTime(){Talk("p1");Talk("p2",true);Assert.That(c.life.messages.Last().text,Does.Contain("déjà").And.Contain("terrain"));Assert.AreEqual(0,c.Person("p2").lastTalk);}
        [Test] public void SmsAndPhoneShareTheSameCollectiveLimit(){Talk("p1");Talk("p2",true);Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
        [Test] public void ChangingLeaderSixDaysLaterDoesNotBypassLimit(){Talk("p1");c.life.day+=6;Talk("p2");Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
        [Test] public void ANewCollectiveMessageBecomesPossibleAfterSevenDays(){Talk("p1");c.life.day+=7;Talk("p2");Assert.AreEqual(75.6,c.Person("p21").morale,.001);}
        [Test] public void OriginalSpeakerStillHasIndividualConversationCooldown(){Talk("p1");Assert.Throws<InvalidOperationException>(()=>Talk("p1"));c.life.day+=7;Talk("p1");Assert.AreEqual(79,c.Person("p1").morale,.001);}
        [Test] public void YoungOrUntrustedPlayerDoesNotUseTheGroupOpportunity(){Talk("p21");c.Person("p2").trust=40;Talk("p2");Talk("p1");Assert.AreEqual(73.3,c.Person("p21").morale,.001);Assert.AreEqual(77,c.Person("p1").morale,.001);}
        [Test] public void InboxPruningCannotResetCollectiveLimit(){Talk("p1");for(int i=0;i<350;i++)c.Mail("Staff","Message "+i,"Routine");Talk("p2");Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
        [Test] public void NativeSaveReloadPreservesCollectiveLimit(){Talk("p1");var loaded=ReadSaved(JsonUtility.ToJson(c));loaded.State.Talk(loaded.Restored,"p2","leadership",true);Assert.AreEqual(75.3,loaded.State.Person("p21").morale,.001);Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
        [Test] public void CalendarEpochMigrationKeepsRemainingCooldown(){Talk("p1");c.MigrateSummerEpoch();Talk("p2");Assert.AreEqual(75.3,c.Person("p21").morale,.001);c.life.day+=7;Talk("p3");Assert.AreEqual(75.6,c.Person("p21").morale,.001);}
        [Test] public void UnusedLegacyClubHasNoFabricatedConversationRestriction(){string json=System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(c),"\"leadershipUntil\"\\s*:\\s*0\\s*,?","");StringAssert.DoesNotContain("leadershipUntil",json);var loaded=ReadSaved(json);c=loaded.State;db=loaded.Restored;Talk("p1");Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
        [Test] public void ManagerBanCannotConsumeTheGroupOpportunity(){c.life.managerBanUntil=2;Assert.Throws<InvalidOperationException>(()=>Talk("p1"));c.life.managerBanUntil=0;Talk("p2");Assert.AreEqual(75.3,c.Person("p21").morale,.001);}
    }
}
