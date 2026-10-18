import {test} from 'node:test';
import assert from 'node:assert/strict';
import {correct,angle} from './FootballLoop.mjs';
const q=deg=>({x:Math.sin(deg*Math.PI/360),y:0,z:0,w:Math.cos(deg*Math.PI/360)});
const sample=()=>({hz:60,joints:[{name:'mixamorig:LeftToeBase'},{name:'other'}],frames:Array.from({length:20},(_,i)=>({root:{x:0,y:1,z:i/10},rotations:[q(i<2?0:10),q(5)]}))});
test('seam closes, original data and root/other joints remain unchanged',()=>{let d=sample(),original=structuredClone(d),{data}=correct(d);assert.deepEqual(d,original);assert.ok(angle(data.frames.at(-1).rotations[0],d.frames[0].rotations[0])<1e-5);data.frames.forEach((f,i)=>{assert.deepEqual(f.root,d.frames[i].root);assert.deepEqual(f.rotations[1],d.frames[i].rotations[1]);if(i<=11)assert.deepEqual(f,d.frames[i]);});});
test('progressive monotonic correction without one-frame snap',()=>{let {data,report}=correct(sample());let errors=data.frames.slice(11).map(f=>angle(f.rotations[0],q(0)));errors.slice(1).forEach((v,i)=>assert.ok(v<=errors[i]+1e-5));assert.ok(report.after.incomingSpeed<1e-5);assert.ok(report.after.peakAngularSpeedDegreesPerSecond<=report.before.peakAngularSpeedDegreesPerSecond);});
test('reject moving endpoints and unknown skeletons',()=>{let d=sample();d.frames.at(-2).rotations[0]=q(8);assert.throws(()=>correct(d),/stationary/);d=sample();d.frames[1].rotations[0]=q(1);assert.throws(()=>correct(d),/stationary/);d=sample();d.joints=[];assert.throws(()=>correct(d),/structure/);});
