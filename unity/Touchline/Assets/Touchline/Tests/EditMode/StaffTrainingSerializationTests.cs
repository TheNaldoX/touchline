using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;
namespace Touchline.Tests
{
    public sealed class StaffTrainingSerializationTests
    {
        [Test] public void UnityRoundtripRestoresSameBeneficiaryAndSingleCompletion()
        {
            Database db;var original=StaffTrainingLifecycleTests.Fixture(out db);var member=original.Staff("assistant");original.TrainStaff("assistant");string id=member.id;int before=member.tactics;
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(original));restored.EnsureStaffMarket(db);Assert.That(restored.life.staffTrainingStaffId,Is.EqualTo(id));
            Assert.That(restored.StaffTrainingBeneficiary().id,Is.EqualTo(id));StaffTrainingLifecycleTests.StaffTick(restored,db,45);Assert.That(restored.Staff("assistant").tactics,Is.EqualTo(System.Math.Min(20,before+1)));
            var twice=JsonUtility.FromJson<Career>(JsonUtility.ToJson(restored));twice.EnsureStaffMarket(db);StaffTrainingLifecycleTests.StaffTick(twice,db,46);Assert.That(twice.Staff("assistant").tactics,Is.EqualTo(restored.Staff("assistant").tactics));Assert.That(twice.life.messages.Count(m=>m.subject=="Formation terminée"),Is.EqualTo(1));
        }
        [Test] public void UnityEmptyLegacyIdentityNeverAttachesToCurrentSameRole()
        {
            Database db;var original=StaffTrainingLifecycleTests.Fixture(out db);original.TrainStaff("assistant");original.life.staffTrainingStaffId=null;
            var restored=JsonUtility.FromJson<Career>(JsonUtility.ToJson(original));restored.EnsureStaffMarket(db);int before=restored.Staff("assistant").tactics;StaffTrainingLifecycleTests.StaffTick(restored,db,1);
            Assert.That(restored.Staff("assistant").tactics,Is.EqualTo(before));Assert.That(restored.StaffTrainingInProgress,Is.False);
        }
    }
}
