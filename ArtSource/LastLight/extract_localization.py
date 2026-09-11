from pathlib import Path
import re,json,hashlib
catalog=json.loads(Path("ArtSource/LastLight/localization-source.json").read_text(encoding="utf-8"))
def key(t):
 k='t'+hashlib.sha1(t.encode()).hexdigest()[:10];catalog[k]=t;return k
def string(s,i):
 start=i; interp=s[i]=='$'; i+=2 if interp else 1; literal='';args=[]
 while i<len(s):
  if s[i]=='\\':literal+=s[i:i+2];i+=2;continue
  if s[i]=='"':
   i+=1
   if not re.search('[\u4e00-\u9fff]',literal):return s[start:i],i
   text=json.loads('"'+literal+'"');k=key(text)
   return ('L.F("'+k+'",'+','.join(args)+')' if args else 'L.K("'+k+'")'),i
  if interp and s[i]=='{' and s[i:i+2]!='{{':
   i+=1;begin=i;depth=0
   while i<len(s):
    if s[i]=='"' or s[i:i+2]=='$"':_,i=string(s,i);continue
    if s[i] in '({[':depth+=1
    if s[i] in ')}]':
     if s[i]=='}' and depth==0:break
     depth-=1
    i+=1
   expr=s[begin:i];fmt='';d=0;cut=None
   for j,ch in enumerate(expr):
    if ch in '([':d+=1
    elif ch in ')]':d-=1
    elif d==0 and ch in ':,':cut=j;break
   if cut is not None:fmt=expr[cut:];expr=expr[:cut]
   args.append(convert(expr));literal+='{'+str(len(args)-1)+fmt+'}';i+=1;continue
  if interp and s[i:i+2] in ['{{','}}']:literal+=s[i:i+2];i+=2;continue
  literal+=s[i];i+=1
 return s[start:i],i
def convert(s):
 out='';i=0
 while i<len(s):
  if s[i:i+2]=='//':
   j=s.find('\n',i);j=len(s) if j<0 else j;out+=s[i:j];i=j;continue
  if s[i:i+2]=='/*':
   j=s.find('*/',i)+2;out+=s[i:j];i=j;continue
  if s[i]=='"' or s[i:i+2]=='$"':v,i=string(s,i);out+=v;continue
  out+=s[i];i+=1
 return out
files=list(Path('Assets/LastLight/Scripts').glob('M*.cs'))+[Path('Assets/LastLight/Scripts/Core/M1Rules.cs'),Path('Assets/LastLight/Editor/M2ChapterBuilder.cs'),Path('Assets/LastLight/Editor/M2ChapterAssets.cs')]
for p in files:
 s=p.read_text(encoding='utf-8');s=convert(s);p.write_text(s,encoding='utf-8')
Path('ArtSource/LastLight/localization-source.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
for k,v in catalog.items(): print(k+'\t'+v.replace('\n','\\n'))
