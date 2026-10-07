import math
import numpy as np
def cross(a,b): return np.array([a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]])
def norm(v): n=np.linalg.norm(v); return v/n if n>1e-9 else v
class MB:
    def __init__(s): s.v=[]; s.t=[]
    def add_quad(s,a,b,c,d):
        i=len(s.v); s.v+= [np.array(a,float),np.array(b,float),np.array(c,float),np.array(d,float)]
        s.t+=[(i,i+1,i+2),(i,i+2,i+3)]
    def add_face(s,center,n,u,su,sv):
        v=cross(u,n); hu=np.array(u)*su*0.5; hv=v*sv*0.5; c=np.array(center)
        s.add_quad(c-hu-hv,c-hu+hv,c+hu+hv,c+hu-hv)
    def add_box(s,center,size,rot=np.eye(3)):
        r=rot@np.array([1,0,0]); u=rot@np.array([0,1,0]); f=rot@np.array([0,0,1]); h=np.array(size)*0.5; c=np.array(center)
        s.add_face(c+r*h[0], r, cross(r,u), size[2], size[1])
        s.add_face(c-r*h[0], -r, cross(-r,u), size[2], size[1])
        s.add_face(c+f*h[2], f, cross(f,u), size[0], size[1])
        s.add_face(c-f*h[2], -f, cross(-f,u), size[0], size[1])
        s.add_face(c+u*h[1], u, r, size[0], size[2])
        s.add_face(c-u*h[1], -u, r, size[0], size[2])
    def add_cyl(s,center,radius,height,segs,caps=True):
        ax=np.array([0,1,0]); ex=np.array([1,0,0]); ez=np.array([0,0,1]); c=np.array(center)
        b=c-ax*height/2; t=c+ax*height/2
        for i in range(segs):
            a0=i/segs*2*math.pi; a1=(i+1)/segs*2*math.pi
            d0=ex*math.cos(a0)+ez*math.sin(a0); d1=ex*math.cos(a1)+ez*math.sin(a1)
            s.add_quad(b+d0*radius,t+d0*radius,t+d1*radius,b+d1*radius)
            if caps:
                j=len(s.v)
                s.v+= [t, t+d1*radius, t+d0*radius]; s.t.append((j,j+1,j+2))
                j=len(s.v)
                s.v+= [b, b+d0*radius, b+d1*radius]; s.t.append((j,j+1,j+2))
    def add_lathe(s,prof,segs,center=(0,0,0)):
        ax=np.array([0,1,0]); ex=np.array([1,0,0]); ez=np.array([0,0,1]); c=np.array(center)
        for j in range(len(prof)-1):
            p0=np.array(prof[j]); p1=np.array(prof[j+1]); tan=p1-p0; nrm=norm(np.array([tan[1],-tan[0]]))
            for i in range(segs):
                a0=i/segs*2*math.pi; a1=(i+1)/segs*2*math.pi
                d0=ex*math.cos(a0)+ez*math.sin(a0); d1=ex*math.cos(a1)+ez*math.sin(a1)
                v00=c+d0*p0[0]+ax*p0[1]; v01=c+d0*p1[0]+ax*p1[1]; v10=c+d1*p0[0]+ax*p0[1]; v11=c+d1*p1[0]+ax*p1[1]
                s.add_quad(v00,v01,v11,v10)
                s.expect=getattr(s,'expect',[])
                n0=d0*nrm[0]+ax*nrm[1]
                s.expect += [n0,n0]
    def add_loft(s,rings,capStart,capEnd):
        n=len(rings[0])
        for r in range(len(rings)-1):
            A=rings[r];B=rings[r+1]
            for i in range(n):
                j=(i+1)%n
                s.add_quad(A[i],B[i],B[j],A[j])
        def cap(ring,end):
            c=sum(ring)/len(ring)
            for i in range(len(ring)):
                j=(i+1)%len(ring)
                a=ring[j] if end else ring[i]; b=ring[i] if end else ring[j]
                k=len(s.v); s.v+=[c,a,b]; s.t.append((k,k+1,k+2))
        if capStart: cap(rings[0],False)
        if capEnd: cap(rings[-1],True)
    def check(s,centroid,label,skip_cap=False):
        bad=0; tot=0
        for (a,b,c) in s.t:
            n=cross(s.v[b]-s.v[a], s.v[c]-s.v[a]); 
            if np.linalg.norm(n)<1e-9: continue
            cen=(s.v[a]+s.v[b]+s.v[c])/3
            out = np.dot(n, cen-centroid)
            tot+=1
            if out<0: bad+=1
        print(label,"triangles",tot,"inward-facing",bad)

def ring(z,yb,yt,hwb,hwt,n,segs):
    cy=(yb+yt)/2; hy=max(0.005,(yt-yb)/2); ex=2/n; pts=[]
    for k in range(segs):
        a=math.pi/2-k/segs*2*math.pi; c=math.cos(a); sn=math.sin(a)
        sx=math.copysign(abs(c)**ex,c); sy=math.copysign(abs(sn)**ex,sn)
        hw=hwb+(hwt-hwb)*((sy+1)/2)
        pts.append(np.array([hw*sx,cy+hy*sy,z]))
    return pts

m=MB(); m.add_box((0,0,0),(1,2,3)); m.check(np.zeros(3),"box")
th=np.radians(37); rot=np.array([[math.cos(th),0,math.sin(th)],[0,1,0],[-math.sin(th),0,math.cos(th)]])
m=MB(); m.add_box((1,2,3),(1,2,3),rot); m.check(np.array([1,2,3.]),"box rotated")
m=MB(); m.add_cyl((0,0,0),1,2,16); m.check(np.zeros(3),"cylinder")
rings=[ring(z,0,1,0.9,0.8,4,16) for z in (-2,-1,0,1,2)]
m=MB(); m.add_loft(rings,True,True); m.check(np.array([0,0.5,0.]),"loft (rings CW along +z)")
# tire lathe: profile (radius, axial)
R=0.3;w=0.2;rimR=0.19
prof=[(rimR*.97,-w/2),(R-.045,-w/2),(R-.012,-w*.42),(R,-w*.25),(R,w*.25),(R-.012,w*.42),(R-.045,w/2),(rimR*.97,w/2)]
m=MB(); m.add_lathe(prof,24)
# outward = away from the tube axis ring centre: compute with torus-like centroid per tri: use point on the tire cross-section centre (radius (R+rimR)/2, axial 0) in same angle
bad=0;tot=0
for (a,b,c) in m.t:
    n=cross(m.v[b]-m.v[a], m.v[c]-m.v[a]); cen=(m.v[a]+m.v[b]+m.v[c])/3
    ang=math.atan2(cen[2],cen[0]); core=np.array([math.cos(ang)*(R+rimR)/2,0,math.sin(ang)*(R+rimR)/2])
    tot+=1
    if np.dot(n,cen-core)<0: bad+=1
print("tire lathe triangles",tot,"inward",bad)
