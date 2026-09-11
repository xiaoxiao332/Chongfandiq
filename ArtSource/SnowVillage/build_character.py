import bpy, math, os
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/SnowVillage/Models')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
parts=[]
def mat(name,c):
    m=bpy.data.materials.new(name); m.diffuse_color=(*c,1); return m
coat=mat('Parka',(.24,.32,.37)); trim=mat('ParkaSeams',(.16,.23,.28)); pants=mat('Trousers',(.18,.24,.29))
boot=mat('BootLeather',(.12,.17,.21)); sole=mat('BootSoles',(.085,.12,.15)); skin=mat('Face',(.65,.43,.32))
orange=mat('RustScarf',(.78,.24,.105)); hat=mat('WoolHat',(.23,.32,.38)); rib=mat('HatRib',(.34,.43,.47)); eye=mat('Eyes',(.055,.075,.09))
def finish(o,name,m,bone):
    o.name=name; o.data.materials.append(m)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bone:
        g=o.vertex_groups.new(name=bone); g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    parts.append(o); return o
def sphere(name,pos,scale,m,bone,segments=12,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=pos)
    o=bpy.context.object; o.scale=scale; return finish(o,name,m,bone)
def box(name,pos,scale,m,bone,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); o=bpy.context.object; o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=o.modifiers.new('TailoredEdges','BEVEL'); mod.width=bevel; mod.segments=1
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,m,bone)
def segment(name,a,b,r1,r2,m,bone):
    mid=(Vector(a)+Vector(b))/2
    bpy.ops.mesh.primitive_cone_add(vertices=10,radius1=r2,radius2=r1,depth=(Vector(b)-Vector(a)).length,location=mid)
    o=bpy.context.object; o.rotation_mode='QUATERNION'; o.rotation_quaternion=Vector((0,0,1)).rotation_difference(Vector(a)-Vector(b))
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return finish(o,name,m,bone)
# Tailored torso with a tapered waist and flared coat hem.
verts=[]; faces=[]; profile=[(.73,.25,.17),(.8,.27,.18),(.99,.225,.155),(1.22,.285,.17),(1.31,.23,.145),(1.36,.15,.12)]
for z,rx,ry in profile:
    for i in range(12):
        a=2*math.pi*i/12; verts.append((rx*math.cos(a),ry*math.sin(a),z))
for j in range(len(profile)-1):
    for i in range(12): faces.append((j*12+i,j*12+(i+1)%12,(j+1)*12+(i+1)%12,(j+1)*12+i))
faces.extend([tuple(reversed(range(12))),tuple(range(60,72))])
mesh=bpy.data.meshes.new('TailoredCoat'); mesh.from_pydata(verts,[],faces); mesh.update()
o=bpy.data.objects.new('WinterParka',mesh); bpy.context.collection.objects.link(o); o.data.materials.append(coat); parts.append(o)
for bn in ['Hips','Spine']: o.vertex_groups.new(name=bn)
for v in mesh.vertices:
    w=max(0,min(1,(v.co.z-.88)/.4)); o.vertex_groups['Spine'].add([v.index],w,'REPLACE'); o.vertex_groups['Hips'].add([v.index],1-w,'REPLACE')
box('Zipper',(0,-.177,1.06),(.025,.014,.43),trim,'Spine',.004)
for side,s in [('L',-1),('R',1)]:
    x=s*.125
    segment('TrouserThigh'+side,(x,0,.83),(x,0,.46),.13,.10,pants,'Thigh.'+side)
    sphere('Knee'+side,(x,0,.46),(.105,.105,.105),pants,'Shin.'+side)
    segment('TrouserShin'+side,(x,0,.46),(x,-.012,.18),.102,.08,pants,'Shin.'+side)
    box('Boot'+side,(x,-.062,.135),(.205,.34,.23),boot,'Foot.'+side,.045)
    box('Sole'+side,(x,-.08,.035),(.21,.35,.055),sole,'Foot.'+side,.015)
    box('BootCuff'+side,(x,.006,.235),(.195,.2,.075),trim,'Shin.'+side,.02)
    a=(s*.255,0,1.26); b=(s*.36,-.005,1.01); c=(s*.39,-.06,.82)
    sphere('Shoulder'+side,a,(.13,.14,.14),coat,'UpperArm.'+side)
    segment('SleeveUpper'+side,a,b,.125,.10,coat,'UpperArm.'+side)
    sphere('Elbow'+side,b,(.105,.108,.105),coat,'Forearm.'+side)
    segment('SleeveLower'+side,b,c,.105,.082,coat,'Forearm.'+side)
    sphere('Mitten'+side,(s*.397,-.065,.775),(.085,.075,.105),boot,'Hand.'+side)
    box('Pocket'+side,(s*.155,-.163,.93),(.135,.032,.12),trim,'Hips',.012)
sphere('Head',(0,-.012,1.49),(.155,.14,.185),skin,'Head')
sphere('Nose',(0,-.155,1.48),(.033,.038,.04),skin,'Head',8,4)
for s in [-1,1]: sphere('Eye',(s*.058,-.139,1.535),(.012,.009,.015),eye,'Head',8,4)
sphere('Beanie',(0,.005,1.62),(.167,.15,.14),hat,'Head')
segment('RibbedHatBand',(0,0,1.61),(0,0,1.55),.165,.164,rib,'Head')
sphere('HatPom',(0,.015,1.758),(.052,.049,.055),orange,'Head',10,6)
sphere('ScarfCollar',(0,-.006,1.355),(.166,.14,.065),orange,'Spine')
box('ScarfTail',(.095,-.185,1.24),(.095,.03,.23),orange,'Spine',.009)
# Full articulated deform skeleton.
bpy.ops.object.armature_add(enter_editmode=True); arm=bpy.context.object; arm.name='WinterRig'; arm.data.edit_bones.remove(arm.data.edit_bones[0])
def bone(n,h,t,parent=None):
    b=arm.data.edit_bones.new(n); b.head=h; b.tail=t
    if parent: b.parent=arm.data.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.2)); bone('Hips',(0,0,.82),(0,0,1.04),'Root'); bone('Spine',(0,0,1.04),(0,0,1.35),'Hips'); bone('Head',(0,0,1.35),(0,0,1.67),'Spine')
for side,s in [('L',-1),('R',1)]:
    x=s*.125
    bone('Thigh.'+side,(x,0,.82),(x,0,.46),'Hips'); bone('Shin.'+side,(x,0,.46),(x,-.012,.18),'Thigh.'+side); bone('Foot.'+side,(x,-.012,.18),(x,-.19,.09),'Shin.'+side)
    bone('UpperArm.'+side,(s*.255,0,1.26),(s*.36,-.005,1.01),'Spine'); bone('Forearm.'+side,(s*.36,-.005,1.01),(s*.39,-.06,.82),'UpperArm.'+side); bone('Hand.'+side,(s*.39,-.06,.82),(s*.40,-.06,.71),'Forearm.'+side)
bpy.ops.object.mode_set(mode='OBJECT'); bpy.ops.object.select_all(action='DESELECT')
for p in parts: p.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); body=bpy.context.object; body.name='WinterTraveler_Skinned'
mod=body.modifiers.new('WinterRigDeformation','ARMATURE'); mod.object=arm; body.parent=arm
for p in arm.pose.bones: p.rotation_mode='XYZ'
def reset():
    for p in arm.pose.bones: p.rotation_euler=(0,0,0); p.location=(0,0,0)
def animate(name,length):
    arm.animation_data_create(); action=bpy.data.actions.new(name); arm.animation_data.action=action
    for f in range(1,length+2):
        reset(); phase=(f-1)/length*2*math.pi
        if name=='Walk':
            arm.pose.bones['Hips'].location.y=-.10+.008*math.sin(phase*2); arm.pose.bones['Spine'].rotation_euler=(.035,.02*math.sin(phase),.02*math.sin(phase))
            for side,offset in [('L',0),('R',math.pi)]:
                p=phase+offset; swing=math.sin(p); cycle=((f-1)/length+offset/(2*math.pi))%1
                # Stance occupies half of the 1.2s loop: .63m / .6s = 1.05m/s.
                if cycle<.5:
                    ankle_y=-.315+1.26*cycle; ankle_z=.18
                else:
                    t=(cycle-.5)*2; ankle_y=.315-.63*(t*t*(3-2*t)); ankle_z=.18+.13*math.sin(t*math.pi)
                vertical=.82+arm.pose.bones['Hips'].location.y-ankle_z
                upper=.36; lower=math.sqrt(.28**2+.012**2); distance=min(math.sqrt(ankle_y**2+vertical**2),upper+lower-.001)
                thigh=math.atan2(ankle_y,vertical)-math.acos(max(-1,min(1,(upper*upper+distance*distance-lower*lower)/(2*upper*distance))))
                bend=math.acos(max(-1,min(1,(distance*distance-upper*upper-lower*lower)/(2*upper*lower))))
                shin=bend+math.atan2(.012,.28)
                arm.pose.bones['Thigh.'+side].rotation_euler.x=thigh
                arm.pose.bones['Shin.'+side].rotation_euler.x=shin
                arm.pose.bones['Foot.'+side].rotation_euler.x=-(thigh+shin)
                arm.pose.bones['UpperArm.'+side].rotation_euler.x=-.32*swing
                arm.pose.bones['Forearm.'+side].rotation_euler.x=-.13-.10*max(0,-swing)
        else:
            arm.pose.bones['Spine'].rotation_euler.x=.016*math.sin(phase); arm.pose.bones['Head'].rotation_euler.z=.025*math.sin(phase)
        for p in arm.pose.bones:
            p.keyframe_insert('rotation_euler',frame=f,group=p.name); p.keyframe_insert('location',frame=f,group=p.name)
    action.use_fake_user=True
    return action
bpy.context.scene.render.fps=30
idle=animate('Idle',90); walk=animate('Walk',36)
for action,end in [(idle,91),(walk,37)]:
    arm.animation_data.action=action; bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=end; bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Traveler_'+action.name+'.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
arm.animation_data.action=idle; bpy.context.scene.frame_end=91; bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(__file__),'WinterTraveler.blend'))
print('CHARACTER_DONE',len(body.data.vertices),'vertices',len(arm.data.bones),'bones')
