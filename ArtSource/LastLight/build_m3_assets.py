"""LastLight M3 only. Background Blender. Never modifies SnowVillage assets."""
import bpy, math, os, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/LastLight/Models/M3'); os.makedirs(OUT,exist_ok=True)
random.seed(19)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
palette={'Timber':(.25,.32,.36),'Edge':(.43,.49,.49),'Metal':(.13,.21,.25),'Rust':(.53,.29,.16),'Snow':(.70,.82,.9),'Warm':(1,.62,.22),'Teal':(.22,.59,.57),'Cloth':(.37,.44,.42),'Dark':(.055,.09,.12),'Bone':(.68,.64,.48),'Bark':(.29,.25,.23),'Food':(.51,.36,.25)}
materials={}
for n,c in palette.items():
 m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); materials[n]=m
parts=[]
def finish(o,n,m):
 o.name=n; o.data.materials.append(materials[m]); parts.append(o); return o
def box(n,p,s,m='Timber',bevel=.035):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=bpy.context.object; o.scale=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Soft edges','BEVEL'); mod.width=bevel; mod.segments=2; bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,n,m)
def orb(n,p,s,m='Metal'):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=p); o=bpy.context.object; o.scale=s
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); return finish(o,n,m)
def rod(n,a,b,r,m='Metal',r2=None):
 a,b=Vector(a),Vector(b); bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r,radius2=r if r2 is None else r2,depth=(b-a).length,location=(a+b)/2)
 o=bpy.context.object; o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(b-a)
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True); return finish(o,n,m)
def export(n):
 bpy.ops.object.select_all(action='DESELECT')
 for p in parts:p.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]
 bpy.ops.object.join(); obj=bpy.context.object; obj.name=n
 bpy.context.scene.cursor.location=(0,0,0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,n+'.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y')
 # Gallery source keeps each exported asset as a named editable mesh.
 obj.location=(len(bpy.data.objects)%6*5, len(bpy.data.objects)//6*5,0); parts.clear()


for name in ['Charger','Decoy','WatchLight','Table','Chair','Shelf','Lamp']:
 if name=='Charger':
  box('Base',(0,0,.15),(1.6,1.4,.3),'Metal');box('Battery',(0,.4,.65),(1,.5,.8),'Teal');rod('Socket',(0,0,.3),(0,0,.6),.2,'Warm')
 elif name=='Decoy':
  box('Platform',(0,0,.15),(1.3,1.3,.3));rod('Pole',(0,0,.3),(0,0,1.5),.06);orb('Lure',(0,0,1.5),(.3,.3,.25),'Food');rod('Lamp',(0,0,1.6),(0,0,1.8),.09,'Warm')
 elif name in ['WatchLight','Lamp']:
  height=2.7 if name=='WatchLight' else 1.3
  box('Foot',(0,0,.1),(.5,.5,.2),'Metal');rod('Pole',(0,0,.2),(0,0,height),.07);orb('Lantern',(0,0,height),(.25,.25,.3),'Warm')
 elif name in ['Table','Chair']:
  height=.8 if name=='Table' else .48;size=1.5 if name=='Table' else .7
  box('Top',(0,0,height),(size,size,.14),'Timber')
  for x in [-1,1]:
   for y in [-1,1]:box('Leg',(x*size*.38,y*size*.38,height/2),(.09,.09,height),'Metal')
  if name=='Chair':box('Back',(0,.3,.9),(.7,.12,.85),'Edge')
 else:
  for z in [.15,.75,1.35]:box('Shelf',(0,0,z),(1.5,.65,.1),'Timber')
  for x in [-.7,.7]:box('Frame',(x,0,.75),(.1,.6,1.6),'Metal')
 export(name)
for name in ['Ya','Shou']:
 if name=='Ya':
  box('Chassis',(0,0,.35),(1.1,.9,.4),'Teal');box('Tray',(0,0,.7),(1.3,1,.12),'Timber')
  for x in [-.5,.5]:
   for y in [-.3,.3]:orb('Wheel',(x,y,.22),(.2,.2,.2),'Dark')
  rod('Neck',(0,0,.7),(0,0,1.1),.08);orb('Leaf hood',(0,0,1.2),(.55,.4,.2),'Teal');orb('Eye',(0,-.36,1.12),(.15,.06,.1),'Warm')
  rod('Plant',(0,.15,.75),(0,.15,1),.025,'Bark')
 else:
  for x in [-.27,.27]:
   box('Foot',(x,0,.1),(.3,.55,.18),'Metal');rod('Leg',(x,0,.2),(x,0,1.15),.065)
  box('Core',(0,0,1.25),(.65,.35,.55),'Teal');rod('Neck',(0,0,1.5),(0,0,1.9),.055);orb('Eye',(0,-.15,1.9),(.25,.17,.25),'Warm')
  for x in [-.45,.45]:rod('Tool arm',(x*.6,0,1.4),(x,-.15,.8),.055)
 export(name)
for name in ['RelayTower','LaunchStructure','CommonPower','Navigation','PersonalShip','Transport','GroundNetwork']:
 if name=='RelayTower':
  for x in [-1,1]:
   for y in [-1,1]:rod('Tower',(x,y,0),(x*.4,y*.4,8),.1)
  for z in [2,4,6]:box('Brace',(0,0,z),(2-z*.15,2-z*.15,.12),'Edge')
  orb('Dish',(0,0,8),(1.7,.3,1.7),'Teal');rod('Antenna',(0,0,8),(0,-2,8),.06,'Warm')
 elif name=='LaunchStructure':
  box('Launch pad',(0,0,.2),(6,6,.4),'Metal')
  for x in [-2.5,2.5]:rod('Rail',(x,0,.4),(x,0,6),.16,'Edge')
  box('Crossbeam',(0,0,5),(5.5,.4,.4),'Metal')
 elif name in ['PersonalShip','Transport']:
  length=3 if name=='PersonalShip' else 6
  orb('Hull',(0,0,1.4),(1.2,length,1.2),'Edge');box('Window',(0,-length*.65,2.1),(1.3,1,.15),'Teal')
  for x in [-1.2,1.2]:
   orb('Engine',(x,length*.7,1),(.45,.8,.45),'Metal');orb('Nozzle',(x,length*.95,1),(.3,.2,.3),'Warm')
   box('Landing foot',(x,0,.25),(.35,2,.5),'Metal')
 elif name=='CommonPower':
  box('Housing',(0,0,.7),(2,2,1.4),'Metal')
  for x in [-.6,0,.6]:rod('Cell',(x,0,.5),(x,0,1.7),.2,'Teal')
 elif name=='Navigation':
  box('Console',(0,0,.6),(2,1,1.2),'Metal');box('Screen',(0,-.4,1.3),(1.7,.15,.8),'Teal')
 else:
  for x in [-3,0,3]:rod('Beacon',(x,0,0),(x,0,2.5),.07);orb('Light',(x,0,2.5),(.25,.25,.35),'Warm')
 export(name)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource/LastLight/M3Assets.blend'))
print('M3 assets exported; M1/M2 and SnowVillage unchanged')
