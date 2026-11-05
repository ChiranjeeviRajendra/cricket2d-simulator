"""CC0 MakeHuman mesh/morph/skin adapter. Blender build-time only."""
import json
from mathutils import Vector, Matrix


def frame(head, tail):
    axis = (tail - head).normalized()
    forward = Vector((0, 0, 1))
    forward = (forward - axis * axis.dot(forward)).normalized()
    side = axis.cross(forward).normalized()
    return Matrix((side, axis, forward)).transposed().to_4x4()


def load(reference):
    vertices, faces, groups = [], [], {}
    group = ''
    for line in (reference / 'makehuman-base.obj').read_text().splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == 'v':
            vertices.append(Vector(tuple(map(float, fields[1:4]))))
        elif fields[0] == 'g':
            group = fields[1]
            groups.setdefault(group, set())
        elif fields[0] == 'f':
            indices = [int(field.split('/')[0]) - 1 for field in fields[1:]]
            groups[group].update(indices)
            if group == 'body':
                faces.append(indices)
    for filename, amount in [('adult-male.target', 1), ('male-average.target', .55), ('male-athletic.target', .45)]:
        for line in (reference / filename).read_text().splitlines():
            fields = line.split()
            if len(fields) == 4 and not line.startswith('#'):
                vertices[int(fields[0])] += Vector(tuple(map(float, fields[1:]))) * amount
    floor = min(vertices[i].y for i in groups['body'])
    scale = 1.8 / (max(vertices[i].y for i in groups['body']) - floor)
    vertices = [Vector((p.x * scale, (p.y - floor) * scale, p.z * scale)) for p in vertices]
    skeleton = json.loads((reference / 'default.mhskel').read_text())
    skin = json.loads((reference / 'default_weights.mhw').read_text())
    assert skeleton['license'] == skin['license'] == 'CC0'

    def joint(name):
        indices = skeleton['joints'][name]
        return sum((vertices[i] for i in indices), Vector()) / len(indices)

    def head(name):
        return joint(skeleton['bones'][name]['head'])

    def tail(name):
        return joint(skeleton['bones'][name]['tail'])

    shoulders = (head('upperarm01.L') + head('upperarm01.R')) / 2
    originals = {
        'pelvis': (head('spine05'), head('spine03')),
        'spine': (head('spine03'), head('spine01')),
        'chest': (head('spine01'), shoulders),
        'neck': (shoulders, head('neck01')),
        'head': (head('neck01'), tail('head')),
    }
    for side, suffix in [('front', 'L'), ('rear', 'R')]:
        originals[side + '_thigh'] = (head('upperleg01.' + suffix), head('lowerleg01.' + suffix))
        originals[side + '_shin'] = (head('lowerleg01.' + suffix), head('foot.' + suffix))
        a = head('foot.' + suffix)
        originals[side + '_foot'] = (a, a + Vector((0, -.05, .14)))
    for side, suffix in [('top', 'L'), ('bottom', 'R')]:
        originals[side + '_upperarm'] = (head('upperarm01.' + suffix), head('lowerarm01.' + suffix))
        originals[side + '_forearm'] = (head('lowerarm01.' + suffix), head('wrist.' + suffix))
        a = head('wrist.' + suffix)
        originals[side + '_hand'] = (a, a + Vector((0, .1, 0)))

    bones = {name: (a.copy(), b.copy()) for name, (a, b) in originals.items()}
    # Retarget limb proportions once in the asset, never stretch a limb during animation.
    for side in ['top', 'bottom', 'front', 'rear']:
        upper, lower = ('upperarm', 'forearm') if side in ['top', 'bottom'] else ('thigh', 'shin')
        length_a, length_b = (.32, .29) if upper == 'upperarm' else (.45, .43)
        a, b = originals[side + '_' + upper]
        _, c = originals[side + '_' + lower]
        new_b = a + (b - a).normalized() * length_a
        new_c = new_b + (c - b).normalized() * length_b
        bones[side + '_' + upper] = (a, new_b)
        bones[side + '_' + lower] = (new_b, new_c)
        end = side + ('_hand' if upper == 'upperarm' else '_foot')
        bones[end] = (new_c, new_c + originals[end][1] - originals[end][0])

    def mapped(name):
        suffix = name.split('.')[-1]
        if name.startswith('upperarm'): return ('top' if suffix == 'L' else 'bottom') + '_upperarm'
        if name.startswith('shoulder01'): return 'chest'
        if name.startswith('lowerarm'): return ('top' if suffix == 'L' else 'bottom') + '_forearm'
        if name.startswith(('wrist', 'finger', 'metacarpal')): return ('top' if suffix == 'L' else 'bottom') + '_hand'
        if name.startswith('upperleg'): return ('front' if suffix == 'L' else 'rear') + '_thigh'
        if name.startswith('lowerleg'): return ('front' if suffix == 'L' else 'rear') + '_shin'
        if name.startswith(('foot', 'toe')): return ('front' if suffix == 'L' else 'rear') + '_foot'
        if name.startswith(('pelvis', 'root', 'spine04', 'spine05')): return 'pelvis'
        if name.startswith(('spine03', 'spine02')): return 'spine'
        if name.startswith(('spine01', 'clavicle', 'breast')): return 'chest'
        if name == 'neck01': return 'neck'
        return 'head'

    weights = [{} for _ in vertices]
    for source, entries in skin['weights'].items():
        target = mapped(source)
        for index, value in entries:
            weights[index][target] = weights[index].get(target, 0) + value
    transforms = {}
    for name, (a, b) in originals.items():
        c, d = bones[name]
        source_frame, target_frame = frame(a, b), frame(c, d)
        source_frame.translation, target_frame.translation = a, c
        target_frame.col[1] *= (d - c).length / (b - a).length
        transforms[name] = target_frame @ source_frame.inverted()
    for i in groups['body']:
        influences = sorted(weights[i].items(), key=lambda pair: pair[1], reverse=True)[:4]
        total = sum(w for _, w in influences)
        weights[i] = {n: w / total for n, w in influences} if total else {'pelvis': 1}
        vertices[i] = sum((transforms[n] @ vertices[i] * w for n, w in weights[i].items()), Vector())
    eyes = []
    for group in ['joint-l-eye', 'joint-r-eye']:
        eyes.append(sum((vertices[i] for i in groups[group]), Vector()) / len(groups[group]))
    return vertices, faces, weights, bones, eyes
