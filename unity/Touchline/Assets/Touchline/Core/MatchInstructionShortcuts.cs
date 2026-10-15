using System;

namespace Touchline.Core
{
    public enum MatchInstruction { Pressing, Line, Width, Tempo, Directness }

    // Values use the same 0–1 scale as the full tactics screen: three broad presets.
    public static class MatchInstructionShortcuts
    {
        public static float Value(int level)
        {
            if(level<0||level>2)throw new ArgumentOutOfRangeException(nameof(level));
            const float Low=.2f,Normal=.5f,High=.8f;
            return level==0?Low:level==1?Normal:High;
        }
        public static float Read(Tactic t,MatchInstruction instruction)
        {
            switch(instruction){case MatchInstruction.Pressing:return t.pressing;case MatchInstruction.Line:return t.line;
                case MatchInstruction.Width:return t.width;case MatchInstruction.Tempo:return t.tempo;case MatchInstruction.Directness:return t.directness;
                default:throw new ArgumentOutOfRangeException(nameof(instruction));}
        }
        public static void Apply(Career career,MatchInstruction instruction,int level)
        {
            if(career==null)throw new ArgumentNullException(nameof(career));
            float value=Value(level);
            switch(instruction){case MatchInstruction.Pressing:career.tactic.pressing=value;break;case MatchInstruction.Line:career.tactic.line=value;break;
                case MatchInstruction.Width:career.tactic.width=value;break;case MatchInstruction.Tempo:career.tactic.tempo=value;break;
                case MatchInstruction.Directness:career.tactic.directness=value;break;default:throw new ArgumentOutOfRangeException(nameof(instruction));}
            career.BindMatchTactic();
        }
    }
}
