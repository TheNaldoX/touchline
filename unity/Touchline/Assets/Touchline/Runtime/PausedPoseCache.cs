using System;
using Touchline.Core;
using UnityEngine;

namespace Touchline
{
    public sealed class PausedPoseCache
    {
        struct Snapshot
        {
            public string id,action,intent,kind;public PlayerMotionContext context;public float fitness,withdraw;public Point position,previous,velocity,target;public float angle,time,stride,height,side,contact;public int sequence;public Vector3 ball;
            static bool Same(Point a,Point b)=>a.x==b.x&&a.z==b.z;
            public bool Matches(Actor a,Vector3 look,PlayerMotionContext motion)=>withdraw==a.tackleWithdrawFrom&&context.Same(motion)&&kind==a.actionKind&&fitness==a.fitness&&id==a.id&&action==a.action&&intent==a.intent&&Same(position,a.position)&&Same(previous,a.previous)&&Same(velocity,a.velocity)&&Same(target,a.actionTarget)&&angle==a.angle&&time==a.actionTime&&stride==a.stride&&height==a.actionHeight&&side==a.diveSide&&contact==a.actionContactTime&&sequence==a.actionSequence&&ball.Equals(look);
            public Snapshot(Actor a,Vector3 look,PlayerMotionContext motion){withdraw=a.tackleWithdrawFrom;context=motion;kind=a.actionKind;fitness=a.fitness;id=a.id;action=a.action;intent=a.intent;position=a.position;previous=a.previous;velocity=a.velocity;target=a.actionTarget;angle=a.angle;time=a.actionTime;stride=a.stride;height=a.actionHeight;side=a.diveSide;contact=a.actionContactTime;sequence=a.actionSequence;ball=look;}
        }
        readonly Snapshot[] states=new Snapshot[22];readonly bool[] valid=new bool[22];
        public void Reset()=>Array.Clear(valid,0,valid.Length);
        public bool NeedsUpdate(int index,Actor actor,Vector3 ball,PlayerMotionContext context=default){if(valid[index]&&states[index].Matches(actor,ball,context))return false;valid[index]=true;states[index]=new Snapshot(actor,ball,context);return true;}
    }
}
