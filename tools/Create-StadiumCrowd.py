"""Generate editable seated spectator sprites. Export the SVG with Inkscape."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
shirts = ['e8e6da', '316ca5', '5584b1', 'bd624e', 'd4b557',
          '447d74', 'a4babd', 'c8cebe', '364a65', '90749a']
skins = ['b77953', 'd6a77d', '82583f', 'edc3a0', 'a27151']
parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1280" viewBox="0 0 1024 1280">']
for direction in range(4):
    for variant in range(50):
        index = direction * 50 + variant
        shirt, skin = shirts[variant % 10], skins[variant // 10]
        parts.append(f'<g id="spectator-{index}" transform="translate({index % 16 * 64},{index // 16 * 96})">')
        if direction == 3:
            parts.append('<g transform="translate(64,0) scale(-1,1)">')
        else:
            parts.append('<g>')
        if direction < 2:
            parts.append('''<path d="M23 54L15 65L19 83M41 54L49 65L45 83" stroke="#384952" stroke-width="9" stroke-linejoin="round" fill="none"/>
<path d="M14 84h10M40 84h10" stroke="#d9dbd4" stroke-width="5" stroke-linecap="round"/>''')
            parts.append(f'<path d="M29 25h6v11h-6z" fill="#{skin}"/>')
            parts.append(f'<path d="M21 32Q32 28 43 32L44 57Q32 62 20 57Z" fill="#{shirt}"/>')
            parts.append(f'<path d="M21 35L17 50M43 35L47 50" stroke="#{shirt}" stroke-width="8" stroke-linecap="round"/>')
            parts.append(f'<path d="M17 50L22 63M47 50L42 63" stroke="#{skin}" stroke-width="5" stroke-linecap="round"/>')
            parts.append(f'<ellipse cx="32" cy="20" rx="7.5" ry="9" fill="#{"30383b" if direction == 1 else skin}"/>')
            if direction == 0:
                parts.append('<path d="M25 19Q22 9 32 10Q42 10 39 20L36 15L26 17Z" fill="#30383b"/>')
                parts.append('<path d="M29 21h1M35 21h1" stroke="#554a42" stroke-width="1"/>')
        else:
            parts.append('''<path d="M37 55L19 62L20 83M42 57L25 67L27 85" stroke="#384952" stroke-width="8" stroke-linejoin="round" fill="none"/>
<path d="M14 85h10M21 87h10" stroke="#d9dbd4" stroke-width="4" stroke-linecap="round"/>''')
            parts.append(f'<path d="M30 25h6v10h-6z" fill="#{skin}"/>')
            parts.append(f'<path d="M29 32Q34 29 40 33L45 57Q36 63 30 57L26 38Z" fill="#{shirt}"/>')
            parts.append(f'<path d="M31 37L29 49" stroke="#{shirt}" stroke-width="8" stroke-linecap="round"/>')
            parts.append(f'<path d="M29 49L18 59L23 62" stroke="#{skin}" stroke-width="5" stroke-linecap="round" fill="none"/>')
            parts.append(f'<path d="M27 11Q40 8 39 23L33 28L25 26L25 22L22 21L25 17Z" fill="#{skin}"/>')
            parts.append('<path d="M26 16L24 12Q31 6 38 13L39 23L34 23L34 15Z" fill="#30383b"/>')
        if variant % 7 == 0:
            parts.append('<path d="M24 15Q25 6 34 8Q41 9 41 16H22Z" fill="#e2e4d9"/>')
        parts.append('</g></g>')
# The same texture batches untextured terrace geometry using this opaque white patch.
parts.append('<rect x="1008" y="1264" width="16" height="16" fill="white"/></svg>')
(root / 'Assets/Field/Source/crowd.svg').write_text('\n'.join(parts), encoding='utf-8')
