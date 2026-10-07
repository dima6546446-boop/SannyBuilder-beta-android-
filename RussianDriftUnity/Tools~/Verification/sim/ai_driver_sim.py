PLAN_DECEL=5.0
GAIN=2.0
BRK_GAIN=0.35
ALAT0=7.0
ALAT1=10.5
DEBUG=False
DBG=False
DBG_T=0
import math, random
import numpy as np
import tire_model_sim as sim
sim.ARC_POWER=1.3; sim.SLIDE_FLOOR=0.72; sim.THR_LOSS=0.4
def cr(p0,p1,p2,p3,t):
    t2=t*t;t3=t2*t
    return 0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3)
rel=[(-115,-80),(-40,-88),(45,-84),(110,-62),(135,-10),(112,50),(60,80),(0,72),(-48,54),(-90,68),(-128,40),(-142,-15),(-136,-58)]
ctrl=[np.array(p,float) for p in rel]
pts=[]
n=len(ctrl)
for i in range(n):
    p0=ctrl[(i-1)%n];p1=ctrl[i];p2=ctrl[(i+1)%n];p3=ctrl[(i+2)%n]
    d=np.linalg.norm(p2-p1); steps=max(2,math.ceil(d/4))
    for s in range(steps): pts.append(cr(p0,p1,p2,p3,s/steps))
pts=np.array(pts); N=len(pts)
cum=np.zeros(N+1)
for i in range(N): cum[i+1]=cum[i]+np.linalg.norm(pts[(i+1)%N]-pts[i])
L=cum[N]
print("path length",round(L),"points",N)
def sample(d):
    d%=L; i=int(np.searchsorted(cum,d,side='right')-1); i=min(i,N-1)
    a=pts[i];b=pts[(i+1)%N]; seg=np.linalg.norm(b-a); t=(d-cum[i])/max(seg,1e-6)
    return a+(b-a)*t, (b-a)/max(seg,1e-6)
def tang_at(d):
    return sample(d)[1]
def curvature(d,span):
    t0=tang_at(d-span); t1=tang_at(d+span)
    a=math.degrees(math.atan2(t0[0]*t1[1]-t0[1]*t1[0], t0[0]*t1[0]+t0[1]*t1[1]))
    return a   # sign irrelevant (abs used)
# note: sim uses (x,z) with yaw clockwise from +z. path points are (x,z)
def run_ai(skill, kw, T=120, label=""):
    c=sim.Car(**kw)
    p,t=sample(L-14)
    c.x,c.z=p[0],p[1]; c.yaw=math.atan2(t[0],t[1]); c.vx=c.vz=0
    c.gear=1
    hand_t=0; kick_cd=0; idx=int(np.searchsorted(cum,L-14)); steer_s=0
    random.seed(1)
    laps=0; lastd=L-14; total=-14; maxlat=0; tstart=None; lap_times=[]; spins=0; off=0
    dt=c.dt; t_now=0
    noiseT=0; noise=0
    while t_now<T:
        # progress
        best=None;bd=1e9
        for k in range(-40,41):
            j=(idx+k)%N; d2=(pts[j][0]-c.x)**2+(pts[j][1]-c.z)**2
            if d2<bd: bd=d2;best=j
        idx=best; dist=cum[idx]
        delta=dist-lastd
        if delta<-L/2: laps+=1; lap_times.append(t_now)
        elif delta>L/2: laps-=1
        lastd=dist
        tr=np.array([math.cos(0)*0,0]) 
        fwd=np.array([math.sin(c.yaw),math.cos(c.yaw)])
        speed=math.hypot(c.vx,c.vz)
        look=min(max(7+speed*(0.5-0.1*skill),9),38)
        tgt,_=sample(dist+look)
        toT=tgt-np.array([c.x,c.z]); desired=toT/np.linalg.norm(toT)
        vel=np.array([c.vx,c.vz]); vdir=vel/np.linalg.norm(vel) if speed>3 else fwd
        def sang(a,b): return math.degrees(math.atan2(a[1]*b[0]-a[0]*b[1], a[0]*b[0]+a[1]*b[1]))
        e=sang(vdir,desired); fwdErr=sang(fwd,desired)
        vf=c.vx*fwd[0]+c.vz*fwd[1]; vr=c.vx*math.cos(c.yaw)-c.vz*math.sin(c.yaw)
        drift=math.degrees(math.atan2(vr,vf)) if (vf>2 and speed>2) else 0
        pp = lambda ang: GAIN*math.degrees(math.atan(2*2.4*math.sin(math.radians(ang))/max(look,4.0)))
        delta_a = pp(fwdErr)*2.0 if speed<4 else drift*0.95+max(-28,min(28,pp(e)))
        # lateral from centreline
        cpt=pts[idx]; tg=tang_at(dist); rgt=np.array([tg[1],-tg[0]])
        lat=float(np.dot(np.array([c.x,c.z])-cpt,rgt)); maxlat=max(maxlat,abs(lat))
        edge=8-2
        if abs(lat)>edge: delta_a+= -math.copysign(1,lat)*min((abs(lat)-edge)*3,14)
        noiseT+=dt
        if noiseT>0.35: noiseT=0; noise=(random.random()-0.5)*(1-skill)*9
        delta_a+=noise
        sf=min(1,speed/48); maxA=c.steer_max*(1-0.68*sf**0.7); maxA=max(8,maxA+(c.steer_max-maxA)*min(1,max(0,(abs(drift)-8)/20)))
        cmd=max(-1,min(1,delta_a/maxA)); rate=(3.5+4*skill)*dt
        steer_s+=max(-rate,min(rate,cmd-steer_s))
        # speed plan
        worst=0
        a=8
        while a<=8+max(25,min(speed*2.2,80)):
            ang=abs(curvature(dist+a,9)); radius=18/math.radians(ang) if ang>1 else 400
            alat=ALAT0+(ALAT1-ALAT0)*skill
            vmax=math.sqrt(alat*radius); vall=math.sqrt(vmax*vmax+2*PLAN_DECEL*a)
            worst=vall if worst==0 else min(worst,vall); a+=8
        vT=max(8,min(worst,32+(55-32)*skill))
        thr=max(0,min(1,(vT-speed)*0.45+(0.28 if abs(drift)>12 else 0)))
        brk=max(0,min(1,(speed-vT)*BRK_GAIN)) if speed>vT+1.0 else 0
        if brk>0: thr=0
        thr*=1-min(1,max(0,(abs(drift)-DRIFT_OK)/DRIFT_RANGE))
        hand=False; kick=False
        kick_cd-=dt
        if hand_t>0: hand_t-=dt; hand=True; kick=True
        aheadT=abs(curvature(dist+28,9))
        if kick_cd<=0 and aheadT>32 and speed>15 and abs(drift)<10 and random.random()<0.06 and skill>0.3:
            hand_t=0.28; kick_cd=2.2; kick=True
        r=c.step(thr,brk,steer_s,hand,kick,assist=False,dassist=True)
        if abs(r['ang'])>85: spins+=1
        if DBG and abs(lat)>7 and int(t_now*60)%6==0 and t_now<DBG_T and DBGCOUNT[0]<45: DBGCOUNT.__setitem__(0,DBGCOUNT[0]+1) or print(round(t_now,1),'d=%d'%dist,'spd',round(speed*3.6),'lat',round(lat,1),'drift',round(drift),'steer',round(steer_s,2),'thr',round(thr,2),'brk',round(brk,2),'vT',round(vT*3.6),'g',c.gear,'ay',round(r['ay'],2),'dA',round(delta_a,1),'e',round(e,1),'fe',round(fwdErr,1))
        if abs(lat)>12: off+=1
        t_now+=dt
    print(label,"laps",laps,"maxlat",round(maxlat,1),"spin-frames",spins,"offtrack-frames",off,"lap times",[round(x) for x in lap_times])
desy=dict(mass=1100,torque=240,redline=7200,peak=4800,grip=1.05,steer=38)


DRIFT_OK=28
DRIFT_RANGE=20
DBGCOUNT=[0]
DBG=False; DBG_T=120; GAIN=2.0; BRK_GAIN=0.5; PLAN_DECEL=5.0

for dok,drng in [(28,20),(22,15),(35,20)]:
    DRIFT_OK=dok; DRIFT_RANGE=drng
    print("drift ok",dok,drng)
    for sk in (0.28,0.55,0.8,1.0):
        run_ai(sk,desy,120,"  skill %.2f"%sk)
