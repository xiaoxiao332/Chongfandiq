import bpy, math, os, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..')); OUT=os.path.join(ROOT,'Assets/SnowVillage/Models'); random.seed(42)
def material(n,c):
    m=bpy.data.materials.get(n) or bpy.data.materials.new(n); m.diffuse_color=(*c,1); return m
wood=material('OldTimber',(.22,.285,.34)); edge=material('TimberEdges',(.31,.38,.43)); dark=material('DarkWood',(.13,.19,.245)); roof=material('RoofMetal',(.24,.32,.40)); snow=material('Snow',(.66,.79,.93)); glass=material('WarmWindow',(.35,.235,.145)); iron=material('Iron',(.18,.24,.30))
def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def cube(n,p,s,m,b=.015,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=bpy.context.object; o.name=n; o.scale=s; o.rotation_euler=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if b:
        mod=o.modifiers.new('WornEdges','BEVEL'); mod.width=b; mod.segments=1; bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(m); return o
def beam(n,a,b,r,m,vertices=8):
    a,b=Vector(a),Vector(b); bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r*.8,depth=(b-a).length,location=(a+b)/2); o=bpy.context.object; o.name=n; o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(b-a); o.data.materials.append(m)
def roof_pair(w,d,eave,peak):
    slope=math.atan2(peak-eave,w/2); length=math.sqrt((w/2)**2+(peak-eave)**2)
    for s in [-1,1]:
        cube('StandingSeamRoof',(s*w/4,0,(eave+peak)/2),(length+.16,d,.10),roof,.01,(0,s*slope,0))
        cube('ThickSnowOnRoof',(s*w/4,0,(eave+peak)/2+.115),(length+.22,d+.12,.17),snow,.055,(0,s*slope,0))
    beam('RidgeCap',(0,-d/2,peak+.17),(0,d/2,peak+.17),.065,snow)
def export(name):
    objs=list(bpy.context.scene.objects)
    root=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(root)
    for o in objs: o.parent=root
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY'},bake_anim=False,axis_forward='-Z',axis_up='Y')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),name+'.blend'))
clear()
cube('ShedCore',(0,0,.93),(2.9,1.9,1.85),dark)
for x in range(13):
    px=-1.37+x*.228
    cube('FrontPlank',(px,-.967,.91),(.215,.075,1.80+random.uniform(-.07,.04)),wood if x%3 else edge)
    cube('RearPlank',(px,.967,.94),(.215,.06,1.82),wood)
for s in [-1,1]:
    for j in range(9): cube('SidePlank',(s*1.46,-.84+j*.21,.94),(.065,.20,1.82),wood)
cube('Door',(0,-1.023,.8),(.86,.09,1.56),dark); cube('DoorBrace',(0,-1.08,.9),(.78,.045,.07),edge,rot=(0,.0,.30))
cube('DoorLatch',(.30,-1.087,.88),(.12,.04,.05),iron)
cube('ShedRoof',(0,0,1.96),(3.18,2.24,.14),roof,rot=(-.075,0,0))
cube('ShedRoofSnow',(0,.24,2.05),(3.13,1.62,.12),snow,.04,(-.075,0,0))
for x in [-1.5,1.5]: cube('CornerPost',(x,-.98,.98),(.10,.11,2.05),edge)
export('Woodshed')
clear()
cube('CabinBody',(0,0,1.9),(6,4.8,3.8),wood)
for i in range(17):
    for s in [-1,1]: cube('HorizontalSiding',(0,s*2.425,.16+i*.215),(6,.08,.19),wood if i%3 else edge,.006)
for s in [-1,1]: cube('CornerTimber',(s*2.96,-2.5,1.92),(.16,.16,3.88),edge)
roof_pair(6.6,5.5,3.9,5.5)
for x in [-1.5,1.45]:
    cube('WindowFrame',(x,-2.50,2.12),(1.55,.16,1.52),edge)
    cube('WindowGlass',(x,-2.595,2.12),(1.32,.035,1.3),glass)
    cube('WindowMullion',(x,-2.63,2.12),(.055,.055,1.35),dark)
    for dz in [-.23,.23]: cube('WindowCrossbar',(x,-2.63,2.12+dz),(1.37,.055,.05),dark)
    cube('WindowSillSnow',(x,-2.65,1.40),(1.7,.32,.11),snow,.03)
cube('Chimney',(1.7,.8,5.13),(.62,.72,1.3),dark)
cube('ChimneyCap',(1.7,.8,5.8),(.8,.9,.12),snow)
export('Cabin')
clear()
beam('WellBase',(0,0,0),(0,0,.75),.73,wood,8)
for s in [-1,1]: cube('RoofSupport',(s*.72,0,1.15),(.14,.16,2.3),edge)
roof_pair(2,1.75,2.05,2.85)
beam('WellWinch',(-.70,0,1.21),(.85,0,1.21),.075,dark)
export('CoveredWell')
clear()
for x in [-1.5,1.5]:
    cube('FencePost',(x,0,.6),(.15,.15,1.2),wood)
    cube('PostSnow',(x,0,1.22),(.2,.2,.09),snow,.025)
for z in [.38,.90]:
    cube('FenceRail',(0,0,z),(3.2,.10,.13),edge)
    cube('RailSnow',(0,0,z+.087),(3.2,.12,.045),snow,.01)
export('FenceSection')
clear()
beam('Pole',(0,0,0),(0,0,7.6),.115,wood)
cube('CrossArm',(0,0,7.13),(1.2,.11,.12),iron)
for s in [-1,1]:
    beam('InsulatorStem',(s*.48,0,7.14),(s*.48,0,7.44),.038,iron)
    beam('Porcelain',(s*.48,0,7.29),(s*.48,0,7.48),.075,edge)
cube('PoleSnow',(0,0,7.2),(1.18,.14,.045),snow)
export('UtilityPole')
clear()
beam('Trunk',(0,0,0),(.15,0,7),.22,wood)
for k in range(11):
    a=k*2.4; h=2.2+k*.35; end=(math.cos(a)*(2.7-k*.09),math.sin(a)*(2.7-k*.09),h+1.6)
    start=(.05,0,h); beam('Branch',start,end,.085,wood,6)
    for j in range(2):
        t=.52+j*.25; b=Vector(start).lerp(Vector(end),t); c=b+Vector((math.cos(a+.8)*.85,math.sin(a+.8)*.85,1.0)); beam('Twig',b,c,.03,wood,5)
export('BareTree')
print('ENVIRONMENT_DONE')
