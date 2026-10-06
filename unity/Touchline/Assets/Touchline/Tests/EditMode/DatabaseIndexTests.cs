using NUnit.Framework;
using Touchline.Core;
using UnityEngine;
namespace Touchline.Tests
{
    public class DatabaseIndexTests
    {
        [Test] public void CareerGrowthAndReplacementCannotLeaveStalePlayers()
        {
            var a=new PlayerData{id="a"};var db=new Database{players=new[]{a}};
            Assert.AreSame(a,db.Find("a"));
            var b=new PlayerData{id="b"};db.players=new[]{a,b};Assert.AreSame(b,db.Find("b"));
            var replacement=new PlayerData{id="a",team="new"};db.players[0]=replacement;Assert.AreSame(replacement,db.Find("a"));
            db.players[0]=new PlayerData{id="c"};Assert.IsNull(db.Find("a"));Assert.AreSame(db.players[0],db.Find("c"));
            db.players=new[]{b};Assert.IsNull(db.Find("c"));Assert.AreSame(b,db.Find("b"));Assert.IsNull(db.Find(null));
        }
        [Test] public void IndexIsDerivedAndIsNotSerialized()
        {
            var db=new Database{players=new[]{new PlayerData{id="a"}}};string before=JsonUtility.ToJson(db);
            db.Find("a");Assert.AreEqual(before,JsonUtility.ToJson(db));
            var loaded=JsonUtility.FromJson<Database>(before);Assert.AreEqual("a",loaded.Find("a").id);
        }
    }
}
