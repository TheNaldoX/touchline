using NUnit.Framework;

namespace Touchline.Tests
{
    public sealed class MatchFreezeTests
    {
        [Test] public void LiveMatchSavesRarelyAndOncePerStoppage()
        {
            Assert.IsFalse(MatchArena.AutosaveDue(false,false,12),"Plus de sauvegarde toutes les 12 s en plein jeu");
            Assert.IsTrue(MatchArena.AutosaveDue(false,false,MatchArena.LiveSaveInterval+1));
            Assert.IsTrue(MatchArena.AutosaveDue(true,false,1),"Un arrêt de jeu déclenche une sauvegarde");
            Assert.IsFalse(MatchArena.AutosaveDue(true,true,1000),"Une seule sauvegarde par arrêt");
        }
        [Test] public void RenderScaleDoesNotOscillate()
        {
            const float target=1f/60;
            Assert.AreEqual(.75f,RenderBudget.NextScale(.8f,target*1.5f,target,1,false),1e-4f,"Images lentes : la résolution baisse");
            Assert.AreEqual(.8f,RenderBudget.NextScale(.8f,target,target,1,false),1e-4f,"Images rapides depuis peu : rien ne change");
            Assert.AreEqual(.85f,RenderBudget.NextScale(.8f,target,target,1,true),1e-4f,"Longtemps rapides : la résolution remonte");
            Assert.AreEqual(.8f,RenderBudget.NextScale(.8f,target*1.2f,target,1,true),1e-4f,"Entre les deux seuils : stable");
        }
    }
}
