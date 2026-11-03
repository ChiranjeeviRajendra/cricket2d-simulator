"""Editable Inkscape sky source. Export with Inkscape's --export-filename option."""
from pathlib import Path
from random import Random

root = Path(__file__).resolve().parents[1]
out = root / 'Assets/Field/Source/sky.svg'
out.parent.mkdir(parents=True, exist_ok=True)
rng = Random(840)
clouds=[]
for cx,cy,scale in [(240,190,1.25),(850,110,.8),(1410,250,1.3),(1850,160,.65),(670,370,.7),(1250,415,.5)]:
    puff=[]
    for j in range(16):
        x=cx+(j-8)*23*scale
        y=cy+rng.uniform(-15,16)*scale
        rx=rng.uniform(35,68)*scale
        ry=rng.uniform(16,43)*scale*(1-abs(j-8)/16)
        puff.append(f'<ellipse cx="{x:.1f}" cy="{y:.1f}" rx="{rx:.1f}" ry="{ry:.1f}"/>')
    clouds.append('<g filter="url(#cloud-edge)" fill="url(#cloud-light)">'+''.join(puff)+'</g>')
out.write_text('''<svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" width="2400" height="1500" viewBox="0 0 2000 1250">
<defs>
 <linearGradient id="sky" x2="0" y2="1"><stop stop-color="#5483b1"/><stop offset=".45" stop-color="#a4cbdc"/><stop offset=".72" stop-color="#e3e9dc"/><stop offset="1" stop-color="#f3e5c6"/></linearGradient>
 <radialGradient id="sun"><stop stop-color="#fff3d5" stop-opacity=".8"/><stop offset="1" stop-color="#fff3d5" stop-opacity="0"/></radialGradient>
 <linearGradient id="cloud-light" x2="0" y2="1"><stop stop-color="#fffdf2" stop-opacity=".93"/><stop offset=".5" stop-color="#f5f5ed" stop-opacity=".85"/><stop offset="1" stop-color="#bed0da" stop-opacity=".45"/></linearGradient>
 <filter id="cloud-edge" x="-.2" y="-.6" width="1.4" height="2.2"><feTurbulence baseFrequency=".008 .018" numOctaves="3" seed="82" result="noise"/><feDisplacementMap in="SourceGraphic" in2="noise" scale="22" xChannelSelector="R" yChannelSelector="G"/><feGaussianBlur stdDeviation="2.1"/></filter>
 <filter id="wisps"><feGaussianBlur stdDeviation="4"/></filter>
</defs>
<g inkscape:groupmode="layer" inkscape:label="Atmosphere" id="atmosphere"><rect width="2000" height="1250" fill="url(#sky)"/><ellipse cx="1740" cy="430" rx="600" ry="500" fill="url(#sun)"/></g>
<g inkscape:groupmode="layer" inkscape:label="High cloud wisps" id="wisps" filter="url(#wisps)" fill="#fffef7" opacity=".17"><path d="M170 75Q380 20 700 65Q400 38 240 98Z"/><path d="M1090 45Q1350 5 1760 50Q1420 25 1190 72Z"/><path d="M25 330Q380 275 600 310Q320 305 40 350Z"/></g>
<g inkscape:groupmode="layer" inkscape:label="Cumulus clouds" id="clouds">'''+''.join(clouds)+'''</g></svg>''',encoding='utf-8')
print(out)
