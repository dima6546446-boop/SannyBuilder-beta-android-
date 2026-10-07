import { makeCar, setSpeed } from './harness.mjs';
const id = process.argv[2]||'kopeyka', v=+(process.argv[3]||18), thr=+(process.argv[4]||0);
const c = makeCar(id); setSpeed(c, v); c.assist = 0;
const dt=1/240;
for (let i=0;i<240*8;i++){
  const t=i*dt; const sd = Math.min(t*2.2, 18);
  c.input.throttle=thr; c.input.steer = (sd*Math.PI/180)/c.spec.maxSteer;
  c.step(dt);
  if (i%120==0) console.log(t.toFixed(1),'v',c.speed.toFixed(1),'beta',(c.driftAngle*57.3).toFixed(1),'w',(c.w*57.3).toFixed(1),'ay',(c.ayF/9.81).toFixed(2),'st',(c.steerAngle*57.3).toFixed(1),'aF',(c.slipAngle[0]*57.3).toFixed(1),'aR',(c.slipAngle[2]*57.3).toFixed(1));
}
