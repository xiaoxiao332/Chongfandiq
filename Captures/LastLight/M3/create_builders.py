from pathlib import Path
root=Path(__file__).resolve().parents[3]
editor=root/'Assets/LastLight/Editor'
for source,target in [('M2ChapterBuilder.cs','M3ChapterBuilder.cs'),('M2ChapterAssets.cs','M3ChapterAssets.cs')]:
 s=(editor/source).read_text(encoding='utf-8-sig').replace('M2ChapterBuilder','M3ChapterBuilder').replace('M2Generated','M3Generated').replace('M2Chapter.unity','M3Journey.unity').replace('LastLight/M2/','LastLight/M3/').replace('LastLight M2 Local','LastLight M3 Local').replace('Tools/LastLight/M2/','Tools/LastLight/M3/').replace('ConfigureM2(root)','ConfigureM3(root)').replace('Builds/LastLight/M2','Builds/LastLight/M3').replace('Captures/LastLight/M2','Captures/LastLight/M3')
 s=s.replace('.Take(10)', '.Take(17)')
 s=s.replace('i<15','i<(build?21:15)').replace('int columns=build?2:3;float width=build?235:365;','int columns=3;float width=build?150:365;').replace('new Vector2(width,67)','new Vector2(width,build?56:67)').replace('y-(i/columns)*77','y-(i/columns)*(build?61:77)').replace('new Vector2(width-18,63)','new Vector2(width-18,build?52:63)')
 s=s.replace('MakeScene(false,true);MakeBoot(global);','MakeScene(false,true);MakeScene(false,false,true);MakeBoot(global);')
 s=s.replace('MakeScene(bool home,bool greenhouse=false)','MakeScene(bool home,bool greenhouse=false,bool relay=false)').replace('greenhouse?"Greenhouse":"Workshop"','relay?"Relay":greenhouse?"Greenhouse":"Workshop"')
 s=s.replace('else if(greenhouse)\n','else if(relay)\n                {\n                    MakeRelay(nodes,beasts);player.transform.position=new Vector3(0,.2f,-25);\n                }\n                else if(greenhouse)\n')
 s=s.replace('AddM2Environment(scene,home,greenhouse,nodes);','AddM2Environment(scene,home,greenhouse,nodes);AddM3Environment(scene,home,greenhouse,relay,nodes,beasts,navigationRoot.transform);')
 s=s.replace('(System.IO.File.Exists("Assets/LastLight/Models/M2/"+name+".fbx")?', '(System.IO.File.Exists("Assets/LastLight/Models/M3/"+name+".fbx")?"Assets/LastLight/Models/M3/":System.IO.File.Exists("Assets/LastLight/Models/M2/"+name+".fbx")?')
 (editor/target).write_text(s,encoding='utf-8')
s=(root/'ArtSource/LastLight/build_m2_assets.py').read_text(encoding='utf-8').split('# All dimensions')[0].replace('Models/M2','Models/M3').replace('M1 only','M3 only')
s+='''
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
'''
(root/'ArtSource/LastLight/build_m3_assets.py').write_text(s,encoding='utf-8')
print('M3 builder and model sources created')
