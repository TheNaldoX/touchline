using System;
using NUnit.Framework;
using UnityEngine;
using Touchline.Core;

namespace Touchline.Tests
{
    public class TicketPreviewTests
    {
        static Career Setup()
        {
            var c=new Career{life=new ClubLife{revenue=20000000},world=new CareerWorld{capacity=30000,ticket=20,supporterTrust=70,lastTicketDay=-7}};
            c.life.facilities.Add(new Facility{kind="stadium",level=2});return c;
        }
        [TestCase(5)] [TestCase(25)] [TestCase(200)]
        public void PreviewMatchesAppliedTariffWithoutChangingCareer(int price)
        {
            var c=Setup();string before=JsonUtility.ToJson(c);float trust=c.world.supporterTrust;var forecast=c.PreviewTicketPrice(price);
            Assert.AreEqual(before,JsonUtility.ToJson(c));c.SetTicketPrice(price);
            Assert.AreEqual(forecast.occupancy,c.Occupancy);Assert.AreEqual(forecast.netIncome,c.GateIncome());Assert.AreEqual(forecast.supporterTrustChange,c.world.supporterTrust-trust);
        }
        [Test] public void PreviewExplainsCooldownAndRejectsInvalidPrices()
        {
            var c=Setup();c.SetTicketPrice(30);var forecast=c.PreviewTicketPrice(40);Assert.AreEqual(c.life.day+7,forecast.availableDay);
            Assert.Throws<ArgumentOutOfRangeException>(()=>c.PreviewTicketPrice(4));Assert.Throws<ArgumentOutOfRangeException>(()=>c.PreviewTicketPrice(251));
            Assert.Throws<InvalidOperationException>(()=>c.SetTicketPrice(40));
        }
    }
}
