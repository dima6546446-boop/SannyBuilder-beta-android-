import { makeCar, setSpeed } from './harness.mjs';
const id=process.argv[2]||'kopeyka', v0=+(process.argv[3]||22), thr=+(process.argv[4]||0.8), st=+(process.argv[5]||0.5), assist=+(process.argv[6]||0.7);
const c=makeCar(id); setSpeed(c,v0); c.assist=assist; const dt=1/240;
for(let i=0;i<240*10;i++){const t=i*dt,inp=c.input;
 inp.steer=-st*(t<0.4?1:1); inp.throttle=t<0.3?0:thr; inp.handbrake=(t>0.35&&t<0.6);
 c.step(dt);
 if(i%60==0)console.log(t.toFixed(2),'v',(c.speed*3.6).toFixed(0),'beta',(c.driftAngle*57.3).toFixed(0),'w',(c.w*57.3).toFixed(0),'st',(c.steerAngle*57.3).toFixed(0),'rpm',Math.round(c.rpm),'g',c.gear);}
