using System.Collections.Generic;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // A derived, per-fixed-step cache. Fitness, morale, tactical fit and
        // performance modifiers are still read on every Skill evaluation.
        // Kept opt-in for repeatable profiling: the interleaved desktop audit
        // did not demonstrate a speedup, so production avoids this cache.
        public bool CacheAttributeReads=false;
        bool readingAttributes;
        int attributeEpoch;
        struct AttributeSample { public int epoch;public PlayerData player;public float value; }
        readonly Dictionary<string,AttributeSample>[] attributeSamples=CreateAttributeSamples();
        static Dictionary<string,AttributeSample>[] CreateAttributeSamples()
        {
            var rows=new Dictionary<string,AttributeSample>[22];
            for(int i=0;i<rows.Length;i++)rows[i]=new Dictionary<string,AttributeSample>(32);
            return rows;
        }
        float MatchAttribute(Actor actor,PlayerData player,string key)
        {
            if(!readingAttributes)return player.Attribute(key);
            var row=attributeSamples[actor.side*11+actor.slot];
            if(row.TryGetValue(key,out var sample)&&sample.epoch==attributeEpoch&&ReferenceEquals(sample.player,player))return sample.value;
            float value=player.Attribute(key);row[key]=new AttributeSample{epoch=attributeEpoch,player=player,value=value};return value;
        }
        void Tick()
        {
            if(!CacheAttributeReads){TickCore();return;}
            readingAttributes=CacheAttributeReads;attributeEpoch++;
            try{TickCore();}finally{readingAttributes=false;}
        }
    }
}
