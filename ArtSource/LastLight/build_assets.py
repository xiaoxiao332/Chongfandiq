"""LastLight M1 only. Background Blender. Never modifies SnowVillage assets."""
import bpy, math, os, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/LastLight/Models'); os.makedirs(OUT,exist_ok=True)
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
for n in ['Floor','Wall','Door','Window','Roof']:
 if n=='Floor':
  for i in range(8):box('Floorboard',(i*.25-.875,0,.07),(.24,2,.14))
  for y in [-.8,.8]:box('Joist',(0,y,.02),(2,.12,.16),'Dark')
 elif n=='Roof':
  box('Roof',(0,0,2.65),(2.12,2.12,.15),'Metal'); box('Snow cap',(0,0,2.75),(2.12,2.12,.1),'Snow')
 else:
  for x in [-.93,.93]:box('Post',(x,0,1.3),(.14,.2,2.6),'Edge')
  box('Lintel',(0,0,2.5),(2,.2,.15),'Edge')
  for i in range(12):
   z=.14+i*.2
   if n=='Door' and z<2.1:
    for x in [-.78,.78]:box('Board',(x,0,z),(.4,.14,.18))
   elif n=='Window' and .9<z<1.9:
    for x in [-.78,.78]:box('Board',(x,0,z),(.4,.14,.18))
   else:box('Board',(0,0,z),(1.8,.14,.18))
  if n=='Window':
   box('Warm glass',(0,0,1.45),(1.08,.05,.9),'Warm'); box('Mullion',(0,-.07,1.45),(.06,.12,.95),'Edge'); box('Sill',(0,0,.98),(1.2,.35,.1),'Edge')
  if n=='Door':
   for x in [-.57,.57]:box('Door jamb',(x,-.05,1.05),(.09,.2,2.1),'Metal')
  box('Snow lip',(0,0,2.6),(2.1,.28,.09),'Snow')
 export(n)
for n in ['Stove','Workbench','Storage','Bed','Barricade','Terminal','Pod','Liang','Beast','Hammer','Wood','Stone','Scrap','Fiber','Ration','Fuel','Parts','Core']:
 if n=='Stove':
  box('Hearth',(0,0,.18),(1.3,1,.35),'Bark'); box('Firebox',(0,0,.65),(.86,.7,.75),'Metal')
  box('Fire',(0,-.36,.65),(.56,.035,.35),'Warm')
  for x in [-.2,0,.2]:box('Grille',(x,-.4,.65),(.04,.04,.4),'Dark')
  rod('Flue',(.25,.12,1),(.25,.12,2.35),.12); box('Hood',(.25,.12,2.4),(.5,.4,.08),'Metal')
 elif n=='Workbench':
  box('Top',(0,0,.9),(1.7,.85,.15),'Edge')
  for x in [-.65,.65]:
   for y in [-.3,.3]:box('Leg',(x,y,.45),(.1,.1,.9),'Metal')
  box('Drawer',(0,.03,.65),(1.2,.6,.25)); box('Vice',(.55,-.15,1.1),(.25,.3,.3),'Metal')
  for i in range(3):rod('Tool',(-.5+i*.2,0,1),(-.45+i*.2,.25,1),.04,'Rust')
 elif n=='Storage':
  box('Crate',(0,0,.55),(1.45,1,1)); box('Lid',(0,0,1.07),(1.53,1.08,.12),'Edge')
  for x in [-.52,.52]:box('Binding',(x,-.52,.57),(.07,.06,1.04),'Metal')
  box('Latch',(0,-.55,.85),(.18,.05,.19),'Rust')
 elif n=='Bed':
  box('Frame',(0,0,.35),(1.05,1.85,.3),'Timber'); box('Blanket',(0,-.2,.6),(.94,1.37,.2),'Cloth'); box('Pillow',(0,.65,.65),(.78,.4,.19),'Edge')
  for x in [-.46,.46]:
   for y in [-.85,.85]:box('Feet',(x,y,.22),(.1,.1,.44),'Metal')
 elif n=='Barricade':
  for x in [-.75,0,.75]:
   rod('Stake',(x,-.35,.08),(x,.18,1.05),.09,'Timber'); rod('Stake',(x,.35,.08),(x,-.18,1.05),.09,'Timber')
  for z in [.42,.78]:box('Rail',(0,0,z),(1.95,.16,.14),'Edge')
 elif n=='Terminal':
  box('Plinth',(0,0,.4),(.65,.6,.8),'Metal'); box('Panel',(0,0,1.1),(.9,.3,.65),'Edge'); box('Screen',(0,-.17,1.14),(.68,.025,.39),'Teal')
  for x in [-.23,0,.23]:orb('Button',(x,-.18,.85),(.05,.025,.04),'Warm')
 elif n=='Pod':
  box('Maintenance cradle',(0,0,.35),(1.5,2.7,.6),'Metal'); box('Cushion',(0,0,.67),(1.1,2.3,.12),'Cloth'); box('Head arch',(0,1.15,1.1),(1.5,.18,1.4),'Edge')
  for x in [-.65,.65]:box('Strip',(x,0,.8),(.06,2.2,.06),'Teal')
 elif n=='Liang':
  box('Track chassis',(0,0,.3),(1.15,.85,.42),'Metal')
  for x in [-.53,.53]:
   box('Tread',(x,0,.23),(.22,.96,.38),'Dark')
   for y in [-.3,0,.3]:rod('Wheel',(x-.12,y,.25),(x+.12,y,.25),.15,'Edge')
  box('Torso',(0,0,.9),(.8,.65,.82),'Cloth'); box('Face',(0,-.345,1.05),(.58,.04,.25),'Dark')
  for x in [-.16,.16]:orb('Optic',(x,-.38,1.07),(.08,.03,.045),'Warm')
  box('Level',(0,-.38,.8),(.42,.045,.09),'Teal'); rod('Antenna',(.3,.1,1.3),(.3,.1,1.7),.025,'Metal')
  for s in [-1,1]:
   orb('Shoulder',(s*.48,0,1.04),(.16,.16,.16),'Rust'); rod('Upper arm',(s*.5,0,1.05),(s*.76,-.05,.76),.08)
   orb('Elbow',(s*.76,-.05,.76),(.12,.12,.12),'Edge'); rod('Forearm',(s*.76,-.05,.76),(s*.65,-.34,.55),.075)
   for x in [-.06,.06]:rod('Gripper',(s*.65+x,-.34,.55),(s*.65+x,-.44,.45),.03)
 elif n=='Beast':
  orb('Body',(0,.1,1.05),(.52,.86,.52),'Bark'); orb('Chest',(0,-.45,1.18),(.5,.45,.6),'Cloth'); orb('Head',(0,-.84,1.38),(.32,.44,.32),'Bark'); orb('Muzzle',(0,-1.18,1.25),(.24,.25,.18),'Dark')
  for s in [-1,1]:
   for y in [-.42,.61]:rod('Leg',(s*.33,y,.92),(s*.38,y-.07,.15),.105,'Bark',.065); box('Hoof',(s*.38,y-.12,.09),(.2,.26,.17),'Dark')
   orb('Eye',(s*.26,-1.04,1.48),(.047,.06,.05),'Warm')
   rod('Antler',(s*.21,-.68,1.59),(s*.43,-.5,2.35),.08,'Bone',.025)
   for k in range(3):rod('Branch',(s*(.28+.07*k),-.6,1.85+k*.17),(s*(.6+.15*k),-.68,2+k*.2),.045,'Bone',.01)
 elif n=='Hammer':
  rod('Handle',(0,0,0),(0,0,.7),.035,'Timber'); box('Head',(0,0,.7),(.45,.17,.18),'Metal'); box('Grip',(0,0,.12),(.09,.09,.24),'Rust')
 elif n=='Wood':
  for i in range(4):rod('Branch',(-.4,-.22+i*.15,.1),(.4,.02+i*.1,.2),.055,'Timber')
 elif n=='Stone':
  for i in range(5):orb('Stone',((i%3)*.25-.25,(i//3)*.22,.13),(.2,.2,.16),'Edge')
 elif n=='Scrap':
  for i in range(4):box('Plate',((i%2)*.28,(i//2)*.3,.1+i*.035),(.5,.4,.07),'Rust' if i%2 else 'Metal')
 elif n=='Fiber':
  for i in range(9):rod('Grass',((i%3)*.1,(i//3)*.1,0),((i%3)*.15,(i//3)*.13,.45),.02,'Cloth',.007)
 elif n=='Ration':
  for i in range(5):orb('Root',((i%3)*.19,(i//3)*.2,.12),(.16,.1,.12),'Food')
 elif n=='Fuel':
  box('Can',(0,0,.27),(.4,.25,.5),'Rust'); box('Handle',(0,0,.55),(.23,.12,.08),'Metal')
 elif n=='Parts':
  box('Toolbox',(0,0,.2),(.58,.37,.38),'Teal'); box('Handle',(0,0,.42),(.25,.08,.13),'Metal')
 else:
  box('Module',(0,0,.35),(.6,.45,.65),'Metal'); orb('Core light',(0,-.25,.4),(.18,.03,.18),'Teal')
 export(n)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'LastLightAssets.blend'))
print('M1_ASSETS_COMPLETE',len(bpy.data.objects))
