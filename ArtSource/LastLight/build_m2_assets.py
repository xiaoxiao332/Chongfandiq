"""LastLight M1 only. Background Blender. Never modifies SnowVillage assets."""
import bpy, math, os, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/LastLight/Models/M2'); os.makedirs(OUT,exist_ok=True)
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

# All dimensions in metres, Z up in Blender; explicit FBX axis conversion.
for n in ['Planter','Protection','Bridge','BridgeBroken','Greenhouse']:
 if n=='Planter':
  box('Soil',(0,0,.42),(1.5,1.5,.35),'Bark')
  for axis in [-1,1]:
   box('Board',(axis*.8,0,.45),(.12,1.7,.7),'Timber');box('Board',(0,axis*.8,.45),(1.7,.12,.7),'Timber')
   for y in [-.8,.8]:box('Corner',(axis*.8,y,.38),(.18,.18,.8),'Edge')
 elif n=='Protection':
  for x in [-.8,0,.8]:
   rod('Arch',(x,-.8,.8),(x,0,1.6),.035);rod('Arch',(x,0,1.6),(x,.8,.8),.035)
  box('Top veil',(0,0,1.6),(1.75,.18,.07),'Teal')
 elif n.startswith('Bridge'):
  for y in [-1.2,1.2]:
   box('Beam',(0,y,.18),(7,.18,.35),'Metal')
   for x in [-3,0,3]:box('Post',(x,y,.7),(.15,.15,1.4),'Edge')
   rod('Rail',(-3.2,y,1.3),(3.2,y,1.3),.055,'Timber')
  for i in range(20):
   if n=='BridgeBroken' and 6<=i<=13:continue
   box('Deck',(-3.2+i*.34,0,.35),(.32,2.6,.18),'Timber')
 else:
  for x in [-7,7]:
   for y in [-9,-3,3,9]:
    box('Upright',(x,y,1.8),(.13,.13,3.6),'Metal')
    rod('Roof rib',(x,y,3.6),(0,y,5.5),.065,'Edge')
   rod('Side beam',(x,-9,3.5),(x,9,3.5),.08,'Metal')
  rod('Ridge',(0,-9,5.5),(0,9,5.5),.08,'Metal')
  for x in [-7,7]:box('Frosted side',(x,0,1.6),(.035,18,2.8),'Teal')
 export(n)
for crop in range(3):
 for stage in range(3):
  h=.2+stage*.3
  for i in range(4):
   x=(i%2)*.7-.35;y=(i//2)*.7-.35
   rod('Stem',(x,y,.6),(x,y,.6+h),.022,'Teal')
   for side in [-1,1]:orb('Leaf',(x+side*.14,y,.63+h*.6),(.2,.07,.09),'Teal')
   if stage==2:
    if crop==0:orb('Potato',(x,y,.64),(.18,.16,.11),'Food')
    elif crop==1:
     for k in range(3):rod('Fibre',(x,y,.7),(x+(k-1)*.12,y,.6+h+.2),.023,'Cloth')
    else:
     for k in range(4):orb('Seed pod',(x+(k%2)*.08,y+(k//2)*.08,.6+h),(.07,.06,.09),'Warm')
  export('Crop'+str(crop)+'Stage'+str(stage))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'M2Assets.blend'))
print('M2_ASSETS_COMPLETE',len(bpy.data.objects))
