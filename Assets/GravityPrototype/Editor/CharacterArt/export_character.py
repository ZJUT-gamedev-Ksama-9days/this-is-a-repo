"""Export supplied PSD parts into 32x48 frame animation; pip install psd-tools Pillow.
Never modifies the source PSDs. Run from project root.
"""
from pathlib import Path
import math, json, hashlib
from PIL import Image
from psd_tools import PSDImage

SOURCE = Path.home() / 'Downloads'
OUT = Path('Assets/GravityPrototype/Art/CharacterPSDs/Animated')
PARTS = [
 ('Tail','尾巴1.psd'),('LeftHand','左手1.psd'),('Body','身体1.psd'),
 ('RightHand','右手1.psd'),('Ring','环1.psd'),('Neck','脖子1.psd'),
 ('LeftWing','左耳翼1.psd'),('LeftHeadset','左耳机1.psd'),
 ('Head','头部1.psd'),('RightWing','右耳翼1.psd'),('RightHeadset','右耳机1.psd')]
# Shared original canvas and joint pivots retain the artist's alignment.
PIVOTS = {'Tail':(864,548),'LeftHand':(752,359),'RightHand':(879,360),
 'Body':(824,428),'Ring':(818,326),'Neck':(824,293),'Head':(824,251),
 'LeftWing':(741,169),'RightWing':(900,173),'LeftHeadset':(718,186),'RightHeadset':(911,182)}
CANVAS = (1200,675)
CROP = (589,-20,1069,700)  # 480x720 -> 32x48, common across every pose

def foreground(path):
 psd=PSDImage.open(path)
 if psd.size != CANVAS: raise ValueError(f'Unexpected source size: {path}')
 im=Image.new('RGBA',CANVAS)
 visible=[]
 for layer in psd:
  if layer.visible and layer.name != '背景' and not layer.is_group():
   im.alpha_composite(layer.composite(force=True)); visible.append(layer.name)
 if im.getbbox() is None: raise ValueError(f'No character pixels: {path}')
 return im,visible

def shift(im,dx,dy):
 result=Image.new('RGBA',CANVAS); result.alpha_composite(im,(round(dx),round(dy)));return result

def compose(parts,state,frame,count):
 phase=frame/count*math.tau; s=math.sin(phase); c=math.cos(phase)
 image=Image.new('RGBA',CANVAS)
 for name,part in parts:
  angle=dx=dy=0
  if state=='Idle':
   if name in ('Head','LeftWing','RightWing','LeftHeadset','RightHeadset'):dy=-6*s
   if name=='Tail':angle=5*s
   if name in ('LeftHand','RightHand'):angle=(2 if name=='LeftHand' else -2)*s
   if name=='Ring':dy=5*c
  elif state=='Walk':
   dy=-10*abs(s)
   if name=='LeftHand':angle=13*s
   if name=='RightHand':angle=-13*s
   if name=='Tail':angle=12*math.sin(phase+1)
   if name in ('Head','LeftWing','RightWing','LeftHeadset','RightHeadset'):dx=5*s
   if name=='Body':angle=2*s
   if name=='Ring':angle=2*s
  elif state=='Float':
   dy=-14*s
   if name=='LeftHand':angle=12+5*c
   if name=='RightHand':angle=-12-5*c
   if name=='Tail':angle=16*s
   if name=='LeftWing':angle=-4*c
   if name=='RightWing':angle=4*c
   if name=='Ring':dy+=10*c
  elif state=='Jump':
   # Launch, extended ascent, apex, falling/landing anticipation.
   reach=[0.3,1.0,0.65,0.15][frame]
   if name=='LeftHand':angle=22*reach
   if name=='RightHand':angle=-22*reach
   if name=='Tail':angle=-16*reach
   if name=='Body':dy=-8*reach
   if name=='Ring':dy=12*reach
   if name in ('Head','LeftWing','RightWing','LeftHeadset','RightHeadset'):dy=6*reach
  elif state=='Death':
   t=frame/(count-1)
   # Deliberate collapse/disassembly, held at final frame; no physics/root motion.
   dy=70*t
   if name=='LeftHand':angle=-40*t;dx=-45*t
   if name=='RightHand':angle=40*t;dx=45*t
   if name=='Tail':angle=65*t
   if name=='Ring':angle=12*t;dy=105*t
   if name in ('Head','LeftWing','RightWing','LeftHeadset','RightHeadset'):dx=25*t;dy=45*t
  transformed=part.rotate(angle,resample=Image.Resampling.NEAREST,center=PIVOTS[name]) if angle else part
  transformed=shift(transformed,dx,dy)
  image.alpha_composite(transformed)
 image=image.crop(CROP).resize((32,48),Image.Resampling.NEAREST)
 if state=='Death':
  t=frame/(count-1); pixels=image.load()
  for y in range(48):
   for x in range(32):
    r,g,b,a=pixels[x,y]
    if a:
     factor=1-.40*t;pixels[x,y]=(round(r*factor),round(g*factor),round(b*(1-.25*t)),a)
 return image

def main():
 OUT.mkdir(parents=True,exist_ok=True)
 parts=[];manifest={'canvas':CANVAS,'crop':CROP,'framePixels':[32,48],'ppu':32,'sources':[]}
 for name,filename in PARTS:
  path=SOURCE/filename;im,layers=foreground(path);parts.append((name,im))
  manifest['sources'].append({'part':name,'file':filename,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'visibleLayers':layers})
  directory=OUT/'Parts';directory.mkdir(exist_ok=True)
  im.crop(CROP).resize((32,48),Image.Resampling.NEAREST).save(directory/(name+'.png'))
 full=SOURCE/'全身1.psd';manifest['reference']={'file':full.name,'sha256':hashlib.sha256(full.read_bytes()).hexdigest()}
 previews=[]
 for state,count in [('Idle',4),('Walk',8),('Jump',4),('Float',8),('Death',7)]:
  directory=OUT/state;directory.mkdir(exist_ok=True)
  frames=[]
  for frame in range(count):
   image=compose(parts,state,frame,count);image.save(directory/f'{state}_{frame:02d}.png');frames.append(image)
  previews.append((state,frames))
 (OUT/'sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 # Contact sheet is a scratch QA artifact, not a game asset.
 sheet=Image.new('RGBA',(32*8,48*5),(50,56,70,255))
 for row,(_,frames) in enumerate(previews):
  for col,im in enumerate(frames):sheet.alpha_composite(im,(col*32,row*48))
 preview=Path('Library/Locus/tmp/character-animation-contact.png');preview.parent.mkdir(parents=True,exist_ok=True)
 sheet.resize((768,720),Image.Resampling.NEAREST).save(preview)
 print('Exported 31 animation frames and 11 aligned parts, all 32x48. Preview:',preview)
if __name__=='__main__':main()
