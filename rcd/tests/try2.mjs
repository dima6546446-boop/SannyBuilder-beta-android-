import { makeCar, setSpeed } from './harness.mjs';
const [,, steerDeg='4', thr='0', v='18'] = process.argv;
const c = makeCar('kopeyka'); setSpeed(c, +v); c.assist = 0;
const dt=1/240;
for (let i=0;i<240*5;i++){
  c.input.throttle=+thr; c.input.steer = (+steerDeg*Math.PI/180)/ (c.spec.maxSteer*0.8);
  c.step(dt);
  if (i%60==0) console.log((i*dt).toFixed(2),'v',c.speed.toFixed(1),'beta',(c.driftAngle*57.3).toFixed(1),'w',(c.w*57.3).toFixed(1),'ay',(c.ayF/9.81).toFixed(2),'st',(c.steerAngle*57.3).toFixed(1),'aF',(c.slipAngle[0]*57.3).toFixed(1),'aR',(c.slipAngle[2]*57.3).toFixed(1),'N',c.loads.map(x=>Math.round(x)).join(','), 'gear',c.gear);
}
