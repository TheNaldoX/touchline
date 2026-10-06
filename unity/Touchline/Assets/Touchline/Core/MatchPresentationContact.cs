namespace Touchline.Core
{
    // Ephemeral presentation evidence for the last simulated step. Never used
    // for decisions, physics, randomness or career serialization.
    public struct KeeperPresentationContact
    {
        public bool valid,caught;
        public string keeper;
        public float clock,fraction,startHeight,height;
        public Point start,impact;
    }
    public sealed partial class MatchSimulation
    {
        public KeeperPresentationContact KeeperContact {get;private set;}
        void RecordKeeperContact(Actor keeper,float fraction,Point impact,float height,bool caught)
        {
            KeeperContact=new KeeperPresentationContact{valid=true,keeper=keeper.id,caught=caught,clock=State.clock,fraction=Mathx.Clamp(fraction,0,1),start=State.ball.previous,startHeight=State.ball.previousHeight,impact=impact,height=height};
        }
    }
}
