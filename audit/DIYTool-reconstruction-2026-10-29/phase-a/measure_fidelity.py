"""Compare offscreen WPF crops to retained original-tool cropped images; never runs vendor code."""
from pathlib import Path
from PIL import Image,ImageChops,ImageStat,ImageDraw,ImageSequence
import json,math,sys
root=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else Path(__file__).resolve().parents[1]
review=Path(sys.argv[1]).resolve();rows=[];tiles=[]
for file in sorted((review/'fidelity').glob('*.rgba')):
 crop=next(p for p in (root/'ground-truth/outputs'/file.stem).glob('*_cropped.*') if p.suffix!='.bin');raw=file.read_bytes();source=Image.open(crop);count=source.n_frames if hasattr(source,'n_frames') else 1
 assert len(raw)==51200*count,(file,len(raw),count)
 total=0;squares=0;maximum=0;within=0
 for n,frame in enumerate(ImageSequence.Iterator(source)):
  expected=Image.alpha_composite(Image.new('RGBA',frame.size,(0,0,0,255)),frame.convert('RGBA')).convert('RGB');actual=Image.frombytes('RGBA',(160,80),raw[n*51200:(n+1)*51200]).convert('RGB');diff=ImageChops.difference(expected,actual);stat=ImageStat.Stat(diff);total+=sum(stat.sum);squares+=sum(stat.sum2);maximum=max(maximum,max(v[1] for v in diff.getextrema()));within+=sum(max(px)<=3 for px in diff.get_flattened_data())
  if n==0:
   tile=Image.new('RGB',(480,108),(18,18,22));ImageDraw.Draw(tile).text((2,2),file.stem,fill='white');tile.paste(expected,(0,26));tile.paste(actual,(160,26));tile.paste(diff.point(lambda x:min(255,x*8)),(320,26));tiles.append(tile)
 samples=12800*count*3;mae=total/samples;psnr=10*math.log10(255**2/(squares/samples)) if squares else None
 row={'case':file.stem,'frames':count,'mean_absolute_error':mae,'max_channel_error':maximum,'psnr_db':psnr,'within_3_levels_percent':within/(12800*count)*100,'passes_mean_tolerance_3':mae<=3};rows.append(row)
canvas=Image.new('RGB',(1440,108*((len(tiles)+2)//3)),(18,18,22))
for i,t in enumerate(tiles):canvas.paste(t,((i%3)*480,(i//3)*108))
canvas.save(root/'phase-a/fidelity-24-cases.png');(root/'phase-a/fidelity-24-cases.json').write_text(json.dumps(rows,indent=2));assert len(rows)==24;assert all(r['passes_mean_tolerance_3'] for r in rows)
print('24 cases;',sum(r['frames'] for r in rows),'frames; max MAE',max(r['mean_absolute_error'] for r in rows),'max channel error',max(r['max_channel_error'] for r in rows))
