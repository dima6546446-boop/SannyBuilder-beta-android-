import math, sys
g=9.81
BIAS=0.7
ARC_POWER=1.0
SLIDE_FLOOR=0.80
THR_LOSS=0.0
def sign(x): return 1.0 if x>=0 else -1.0
def clamp(x,a,b): return max(a,min(b,x))
def lat_curve(a):
    peak,slide,floor=0.14,0.50,SLIDE_FLOOR
    if a<peak: return a/peak
    x=clamp((a-peak)/(slide-peak),0,1)
    return 1+(floor-1)*(x*x*(3-2*x))

class Car:
    def __init__(s, mass=1020, L=2.42, wf=0.52, track=1.36, R=0.30, torque=185, redline=6600, peak=4300, idle=900,
                 gears=(3.5,2.1,1.4,1.05,0.82), fd=4.1, grip=1.0, lock=0.5, brake=1020*8.5, awd=False, steer=36):
        s.m=mass; s.L=L; s.a=L*0.48; s.b=L*0.52; s.t=track; s.R=R; s.T=torque; s.red=redline; s.peak=peak; s.idle=idle
        s.gears=gears; s.fd=fd; s.mu0=grip; s.lock=lock; s.brake=brake; s.awd=awd; s.steer_max=steer
        s.Iz=mass/12*(1.7**2+4.1**2)*0.95*1.15
        s.x=s.z=0; s.yaw=0; s.vx=0; s.vz=0; s.w=0   # world, yaw about up, velocity, yaw rate (clockwise +)
        s.gear=1; s.rpm=idle; s.steer=0; s.slips=[0]*4; s.dt=1/60
        s.ax=s.ay=0
    def tq(s,r):
        if r<s.idle: return 0.55
        if r<s.peak:
            x=clamp((r-s.idle)/(s.peak-s.idle),0,1); return 0.62+0.38*(x*x*(3-2*x))
        y=clamp((r-s.peak)/max(100,s.red-s.peak),0,1); return 1-0.32*y
    def step(s, throttle, brake, steer_in, hand, kick=False, assist=True, dassist=True):
        dt=s.dt
        fwd=(math.sin(s.yaw),math.cos(s.yaw)); right=(math.cos(s.yaw),-math.sin(s.yaw))
        vf=s.vx*fwd[0]+s.vz*fwd[1]; vr=s.vx*right[0]+s.vz*right[1]
        speed=math.hypot(s.vx,s.vz)
        # drift angle: signed angle from fwd to vel (clockwise +)
        if speed>2 and vf>0:
            ang=math.degrees(math.atan2(vr, vf))
        else: ang=0
        # steering
        sf=clamp(speed/48,0,1); maxA=s.steer_max*(1-0.68*sf**0.7); maxA=maxA+(s.steer_max-maxA)*clamp((abs(ang)-8)/20,0,1)
        target=steer_in*maxA
        if assist and speed>3 and vf>0:
            gate=clamp((abs(ang)-3)/9,0,1); gate=gate*gate*(3-2*gate)*clamp((speed-3)/6,0,1)
            counter=clamp(ang*0.9,-s.steer_max,s.steer_max)
            target=clamp(counter*gate+target*(1-0.42*gate),-s.steer_max,s.steer_max)
        rate=(260 if abs(target)>abs(s.steer) else 340)*(1-0.4*sf)
        d=target-s.steer; s.steer+=clamp(d,-rate*dt,rate*dt)
        # engine
        ratio=s.gears[s.gear-1]*s.fd
        wheel_rpm=abs(vf)/s.R*9.5493
        s.rpm=max(s.idle, wheel_rpm*ratio) if speed>1 else s.idle+throttle*2500
        hold = dassist and abs(ang)>10 and throttle>0.5
        if s.rpm>s.red*(0.97 if hold else 0.93) and s.gear<len(s.gears): s.gear+=1
        if s.rpm<s.red*0.4 and s.gear>1 and not hold: s.gear-=1
        eng=s.T*s.tq(s.rpm)*throttle*(1.7 if kick else 1.0)
        if s.rpm>=s.red: eng=0
        F=eng*ratio*0.88/s.R*ARC_POWER
        # loads (static + transfer from last accel)
        N=[]
        for i in range(4):
            front=i<2; sideR=(i%2==1)
            base=s.m*g*((s.b if front else s.a)/s.L)/2
            dl=s.m*s.ay*0.45/s.t   # lateral transfer approx (cog 0.45)
            dlg=s.m*s.ax*0.45/s.L
            n=base+(dl if sideR else -dl)*0.5*0+ (s.m*(-s.ay)*0.45/s.t*(0.5))*(1 if sideR else -1)*(-1)
            n+= (-dlg if front else dlg)*0.5
            N.append(max(100,n))
        fxs=0; fzs=0; torque=0
        slips=[]
        meff=s.m*0.25
        for i in range(4):
            front=i<2; sideR=(i%2==1)
            px=(s.t/2 if sideR else -s.t/2); pz=(s.a if front else -s.b)
            delta=math.radians(s.steer) if front else 0
            fdir=(math.sin(s.yaw+delta),math.cos(s.yaw+delta)); rdir=(math.cos(s.yaw+delta),-math.sin(s.yaw+delta))
            # wheel point velocity: v + w x r ; body frame offset (px right, pz fwd) -> world
            offx=px*right[0]+pz*fwd[0]; offz=px*right[1]+pz*fwd[1]
            # yaw rate clockwise positive (w): velocity of point = v + w*(offz, -offx)
            pvx=s.vx+s.w*offz; pvz=s.vz-s.w*offx
            vF=pvx*fdir[0]+pvz*fdir[1]; vR=pvx*rdir[0]+pvz*rdir[1]
            mu=s.mu0
            latmu=mu
            drive=0
            if (not front) or s.awd:
                share=(0.65 if s.awd else 1.0) if not front else 0.35
                base=F*share*0.5
                drive=base
            brk=brake*s.brake*(BIAS if front else 1-BIAS)*0.5
            hb=hand and not front
            if hb: brk+=s.m*g*0.45; latmu*=0.34
            if (not front) and speed>8 and THR_LOSS>0: latmu*=1-THR_LOSS*throttle*clamp((abs(ang)-6)/14,0,1)
            brakeMag=min(brk, abs(vF)*meff/dt)
            brakeMag=min(brakeMag, mu*N[i]*0.95)
            fxw=drive-sign(vF)*brakeMag
            alpha=math.atan2(abs(vR),max(abs(vF),1.0))
            maxLat=latmu*N[i]
            fyw=-sign(vR)*maxLat*lat_curve(alpha)
            cap=abs(vR)*meff/dt*0.6
            if abs(fyw)>cap: fyw=sign(fyw)*cap
            ex=abs(fxw)/(mu*N[i]); ey=abs(fyw)/maxLat if maxLat>1 else 0
            mag=math.hypot(ex,ey)
            fx,fy=fxw,fyw
            if mag>1: fx/=mag; fy/=mag
            slips.append(clamp((alpha-0.10)/0.22,0,1)*clamp(abs(vR)/2.5,0,1))
            Fx=fdir[0]*fx+rdir[0]*fy; Fz=fdir[1]*fx+rdir[1]*fy
            fxs+=Fx; fzs+=Fz
            # torque about vertical (clockwise positive): r x F with r=(offx,offz): torque_y = offz*Fx - offx*Fz  (up axis, clockwise)
            torque+=offz*Fx-offx*Fz
        # aero
        cd=0.5*1.2*0.36*(1.62*1.4*0.78)
        if speed>0.1:
            fxs+=-s.vx/speed*cd*speed*speed; fzs+=-s.vz/speed*cd*speed*speed
        # drift assist yaw damping
        if dassist and speed>6 and abs(ang)>48 and vf>0:
            k=clamp((abs(ang)-48)/30,0,1)
            same=1 if sign(s.w)==-sign(ang) else 0
            torque+=-s.w*s.m*1.4*k*same
        # accel in body frame for transfer
        axw=fxs/s.m; azw=fzs/s.m
        s.ax=axw*fwd[0]+azw*fwd[1]; s.ay=axw*right[0]+azw*right[1]
        s.vx+=fxs/s.m*dt; s.vz+=fzs/s.m*dt
        s.w+=torque/s.Iz*dt
        s.w*=(1-0.35*dt)
        s.yaw+=s.w*dt; s.x+=s.vx*dt; s.z+=s.vz*dt
        s.slips=slips
        return dict(speed=speed*3.6, ang=ang, rpm=s.rpm, gear=s.gear, steer=s.steer, w=math.degrees(s.w), slip_r=(slips[2]+slips[3])/2, ay=s.ay/g)

def run(label, car, script, T, verbose=True):
    print("==",label)
    t=0
    out=[]
    while t<T:
        cmd=script(t,car)
        r=car.step(*cmd)
        if int(t*60)%30==0 and verbose: out.append((round(t,1),)+tuple(round(v,1) for v in r.values()))
        t+=car.dt
    for o in out: print(o)
    return car

if __name__=="__main__":
    # 1. steady-state cornering: speed 20 m/s, steer 8 deg-ish
    c=Car(); c.vz=20.0
    def s1(t,c):
        # hold speed ~ with light throttle
        return (0.25,0,0.25,False)
    run("steady corner 72kmh, steer 0.25", c, s1, 4)
    # 2. throttle breakaway: from 10 m/s full throttle with steer
    c=Car(); c.vz=12.0; c.gear=2
    def s2(t,c): return (1.0,0,0.3 if t>0.5 else 0,False)
    run("power oversteer 2nd gear full throttle + turn", c, s2, 5)
    # 3. handbrake initiation at 22 m/s then throttle
    c=Car(); c.vz=22.0; c.gear=3
    def s3(t,c):
        if t<0.3: return (0.0,0,0.0,False)
        if t<0.6: return (0.2,0,0.8,True)
        return (0.9,0,0.3,False)
    run("handbrake entry then throttle", c, s3, 8)
