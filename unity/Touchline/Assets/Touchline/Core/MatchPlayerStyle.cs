using System;

namespace Touchline.Core
{
    public sealed partial class MatchSimulation
    {
        // Abilities guide a player's preference between feasible options.
        // These bounded utilities cannot override the lane/offsides/tactics
        // checks or the existing priority given to an open close-range finish.
        float CarryPreference(Actor player)
        {
            float carry=Skill(player,"dribbling")*.60f+Skill(player,"ballControl")*.25f+Skill(player,"agility")*.15f;
            float distribute=Skill(player,"shortPassing")*.50f+Skill(player,"longPassing")*.20f+Skill(player,"vision")*.30f;
            return Mathx.Clamp((carry-distribute)/12,-3,3);
        }
        float PassPreference(Actor player,string kind,float distance)
        {
            string key=kind=="cross"?"crossing":distance>27?"longPassing":"shortPassing";
            float distribute=Skill(player,key)*.55f+Skill(player,"vision")*.30f+Skill(player,"ballControl")*.15f;
            float carry=Skill(player,"dribbling")*.65f+Skill(player,"ballControl")*.35f;
            return Mathx.Clamp((distribute-carry)/14,-2.5f,2.5f);
        }
        float OpenPlayShotSkill(Actor player,float distance)
        {
            // Finishing in the box, progressively long shots outside it.
            // Distance already lowers shot probability; this only selects the
            // relevant execution attribute instead of treating them identically.
            float range=Mathx.Clamp((distance-18)/10,0,1);
            float skill=Skill(player,"finishing")*(1-range)+Skill(player,"longShots")*range;
            float pressure=Mathx.Clamp(4-Space(player.position,1-player.side,true),0,4);
            return Mathx.Clamp(skill-(100-Skill(player,"composure"))*pressure*.045f,5,99);
        }
        float DistantShotPreference(Actor player,float distance)
        {
            float range=Mathx.Clamp((distance-18)/10,0,1);
            return Mathx.Clamp(1+(Skill(player,"longShots")-Skill(player,"finishing"))*range/160,.75f,1.2f);
        }
    }
}
