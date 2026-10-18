// Offline prototype adapter. Never edits the source FBX or Touchline runtime.
import fs from 'node:fs';
import { pathToFileURL } from 'node:url';
const keys=['x','y','z','w'];
export function normalized(q){const n=Math.hypot(...keys.map(k=>q[k]));if(!Number.isFinite(n)||n<1e-8)throw Error('Invalid quaternion');return Object.fromEntries(keys.map(k=>[k,q[k]/n]));}
export function angle(a,b){a=normalized(a);b=normalized(b);return 2*Math.acos(Math.min(1,Math.abs(keys.reduce((s,k)=>s+a[k]*b[k],0))))*180/Math.PI;}
function slerp(a,b,t){a=normalized(a);b=normalized(b);let dot=keys.reduce((s,k)=>s+a[k]*b[k],0);if(dot<0){b=Object.fromEntries(keys.map(k=>[k,-b[k]]));dot=-dot;}let x=1-t,y=t;if(dot<.9995){let theta=Math.acos(Math.min(1,dot)),sin=Math.sin(theta);x=Math.sin((1-t)*theta)/sin;y=Math.sin(t*theta)/sin;}return normalized(Object.fromEntries(keys.map(k=>[k,x*a[k]+y*b[k]])));}
export function metrics(d,j){const q=d.frames.map(f=>f.rotations[j]);return {seamDegrees:angle(q[0],q.at(-1)),peakAngularSpeedDegreesPerSecond:Math.max(...q.slice(1).map((v,i)=>angle(q[i],v)*d.hz)),incomingSpeed:angle(q.at(-2),q.at(-1))*d.hz,outgoingSpeed:angle(q[0],q[1])*d.hz};}
export function correct(d){
 const joint=d.joints.findIndex(j=>j.name==='mixamorig:LeftToeBase');
 if(joint<0||d.hz!==60||d.frames.length<10)throw Error('Unsupported clip structure');
 const last=d.frames.length-1,start=last-8; // 133 ms stationary toe plateau in this sprint.
 const before=metrics(d,joint);
 if(before.seamDegrees<1||before.seamDegrees>15)throw Error('Unexpected seam; no automatic repair');
 // This adapter only handles stationary endpoints, not moving contacts or arbitrary curves.
 if(before.outgoingSpeed>.1)throw Error('Start is not stationary');
 for(let i=start+1;i<=last;i++)if(angle(d.frames[i-1].rotations[joint],d.frames[i].rotations[joint])*d.hz>.1)throw Error('End is not stationary');
 const result=structuredClone(d),a=d.frames[start].rotations[joint],b=d.frames[0].rotations[joint];
 for(let i=start+1;i<=last;i++){let t=Math.min(1,(i-start)/(last-start-1));let smooth=t*t*t*(10+t*(-15+6*t));result.frames[i].rotations[joint]=slerp(a,b,smooth);}
 return {data:result,report:{joint:d.joints[joint].name,startFrame:start,endFrame:last,before,after:metrics(result,joint),limitation:'Offline toe flexion only. Toe base and ankle positions unchanged; toe tip/contact surface and Humanoid retargeting still require review.'}};
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const [input,output]=process.argv.slice(2);if(!input||!output)throw Error('Usage: node FootballLoop.mjs input.json output.json');
 const result=correct(JSON.parse(fs.readFileSync(input,'utf8')));
 fs.writeFileSync(output,JSON.stringify(result.data),{flag:'wx'});
 fs.writeFileSync(output+'.report.json',JSON.stringify(result.report,null,2),{flag:'wx'});console.log(JSON.stringify(result.report,null,2));
}
