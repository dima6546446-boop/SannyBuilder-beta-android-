import { makeCar, setSpeed } from '../harness.mjs';
const id = process.argv[2]||'kopeyka', v0=+(process.argv[3]||20), target=+(process.argv[4]||35), assist=+(process.argv[5]||0);
const c = makeCar(id); setSpeed(c, v0); c.assist = assist;
const dt=1/240; let thrI=0.6;
const dir=-1; // влево
for (let i=0;i<240*10;i++){
  const t=i*dt; const inp=c.input;
  const betaD = c.driftAngle*57.3*(-dir); // положительный = занос в сторону поворота
  if (t<0.4) { inp.throttle=0.0; inp.steer=0.35*dir; inp.handbrake=false; }
  else if (t<0.65) { inp.throttle=0.5; inp.steer=0.35*dir; inp.handbrake=true; }
  else {
    inp.handbrake=false;
    // водитель: руль против заноса пропорционально углу + демпфирование рысканья
    const wDeg=c.w*57.3*dir*-1; // >0 если вращаемся в сторону заноса
    const betaF=Math.atan2(c.latSpeed + c.w*c.a, c.fwdSpeed); const wantSteer = (betaD>2) ? Math.max(-1,Math.min(1, betaF*0.95/c.spec.maxSteer)) : dir*0.3;
    inp.steer = wantSteer;
    // газ держит угол
    const wD=c.w*57.3*dir*-1;
    const e=target-betaD; thrI += e*0.0015; thrI=Math.max(0,Math.min(1,thrI));
    inp.throttle=Math.max(0,Math.min(1,thrI + e*0.02 + (wD+ (0))*0.0));
  }
  c.step(dt);
  if (i%60==0) console.log(t.toFixed(2),'v',(c.speed*3.6).toFixed(0),'beta',(c.driftAngle*57.3).toFixed(0),'w',(c.w*57.3).toFixed(0),'st',(c.steerAngle*57.3).toFixed(0),'thr',inp.throttle.toFixed(2),'rpm',Math.round(c.rpm),'g',c.gear);
}
