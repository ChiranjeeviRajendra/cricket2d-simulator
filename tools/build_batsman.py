"""Build the cricket athlete from a continuous CC0 human surface and original equipment.
Run Blender headlessly. The 18-bone skin is exported without animation; runtime owns the bat.
"""
import bpy
import bmesh
import json
import math
import sys
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Art' / 'Batsman'
OUT = ROOT / 'Assets' / 'Characters'
sys.path.insert(0, str(Path(__file__).parent))
from batsman_anatomy import load
(ROOT / 'artifacts').mkdir(exist_ok=True)
sys.stdout = open(ROOT / 'artifacts' / 'batsman-blender.log', 'w', buffering=1)
sys.stderr = sys.stdout
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts = []


def v(point):
    return Vector((point[0], -point[2], point[1]))


def material(name, colour, roughness=.8, metallic=0):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*colour, 1)
    if result.node_tree is None:
        result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*colour, 1)
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Specular IOR Level'].default_value = .25
    return result


jersey = material('Royal blue woven jersey', (.018, .115, .29), .95)
trousers = material('Navy blue trousers', (.012, .052, .14), .95)
navy = material('Navy helmet', (.008, .020, .052), .48)
leather = material('Off-white pad and glove leather', (.68, .71, .67), .8)
seam = material('Pad channels and sole', (.26, .31, .34), .95)
skin = material('Skin', (.30, .145, .075), .87)
amber = material('Warm orange kit trim', (.86, .22, .025), .8)
willow = material('Willow blade', (.62, .43, .22), .8)
steel = material('Steel face guard', (.24, .29, .32), .34, .65)
rubber = material('Grip and shoe trim', (.013, .018, .024), .9)


def finish(obj, name, mat, assignments):
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    for bone, entries in assignments.items():
        group = obj.vertex_groups.new(name=bone)
        for index, weight in entries:
            if weight > 0:
                group.add([index], weight, 'REPLACE')
    parts.append(obj)
    return obj


def mesh_part(name, points, faces, mat, bone):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([v(point) for point in points], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, mat, {bone: [(i, 1) for i in range(len(points))]})


def ellipsoid(name, centre, radius, mat, bone, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=v(centre))
    obj = bpy.context.object
    obj.scale = (radius[0], radius[2], radius[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, {bone: [(i, 1) for i in range(len(obj.data.vertices))]})


def tube(name, points, radius, mat, bone, sides=8):
    points = [Vector(p) for p in points]
    vertices, faces = [], []
    for index, p in enumerate(points):
        axis = (points[min(index + 1, len(points) - 1)] - points[max(0, index - 1)]).normalized()
        across = axis.cross(Vector((0, 0, 1)))
        if across.length < .1:
            across = axis.cross(Vector((1, 0, 0)))
        across.normalize()
        other = axis.cross(across).normalized()
        for j in range(sides):
            angle = math.tau * j / sides
            vertices.append(p + radius * (across * math.cos(angle) + other * math.sin(angle)))
    for i in range(len(points) - 1):
        for j in range(sides):
            faces.append((i*sides+j, i*sides+(j+1)%sides, (i+1)*sides+(j+1)%sides, (i+1)*sides+j))
    faces += [tuple(reversed(range(sides))), tuple((len(points)-1)*sides+j for j in range(sides))]
    return mesh_part(name, vertices, faces, mat, bone)


vertices, faces, weights, bones, eyes = load(SOURCE / 'Reference')
# Closed gloves and shoes replace the open source hands/feet.
faces = [face for face in faces if not all(
    sum(value for name, value in weights[index].items() if name.endswith(('_hand', '_foot'))) > .65 for index in face)]
used = sorted({index for face in faces for index in face})
remap = {original: index for index, original in enumerate(used)}


def garment(index):
    p, w = vertices[index], weights[index]
    if p.y < 1.035:
        return 2
    arm = max(w.get('top_upperarm', 0), w.get('bottom_upperarm', 0))
    if arm > .2:
        side = 'top' if p.x > 0 else 'bottom'
        a, b = bones[side + '_upperarm']
        fraction = (p-a).dot(b-a) / (b-a).length_squared
        return 1 if fraction < .58 else 0
    if max(w.get('top_forearm', 0), w.get('bottom_forearm', 0)) > .2:
        return 0
    if p.y > 1.49 or (p.y > 1.455 and abs(p.x) < .052 and p.z > .04):
        return 0
    return 1


classes = {i: garment(i) for i in used}
# Give the shirt a fabric silhouette, bridging chest/abdominal surface details.
# The original topology and skin weights are retained for smooth joint deformation.
for i in used:
    p=vertices[i]
    if classes[i]==1 and 1.03<p.y<1.405 and abs(p.x)<.185:
        t=max(0,min(1,(p.y-1.035)/.37))
        width=.165+.025*t
        depth=.112+.018*t
        centre=-.006
        angle=math.atan2((p.z-centre)/depth,p.x/width)
        p.x=width*math.cos(angle)
        p.z=centre+depth*math.sin(angle)
neighbors = {i: set() for i in used}
for face in faces:
    for a, b in zip(face, face[1:] + face[:1]):
        neighbors[a].add(b)
        neighbors[b].add(a)
# Clothing has its own looser surface, retaining the source's continuous shoulder/elbow topology.
for _ in range(18):
    relaxed = {}
    for i in used:
        if classes[i] == 0:
            continue
        adjacent = [j for j in neighbors[i] if classes[j] == classes[i]]
        if adjacent:
            relaxed[i] = vertices[i].lerp(sum((vertices[j] for j in adjacent), Vector()) / len(adjacent), .35)
    for i, p in relaxed.items():
        vertices[i] = p
mesh = bpy.data.meshes.new('Continuous athlete surface')
mesh.from_pydata([v(vertices[i]) for i in used], [], [[remap[i] for i in face] for face in faces])
mesh.update()
for local, original in enumerate(used):
    if classes[original]:
        p = vertices[original]
        fold = .002 * math.sin(p.y * 75 + p.x * 25) * (1 if p.y < 1.07 else .2)
        mesh.vertices[local].co += mesh.vertices[local].normal * ((.018 if classes[original] == 1 else .022) + fold)
        if classes[original]==1 and 1.04<p.y<1.405 and abs(p.x)<.19:
            # Reproject after offsets: dense anatomical features must not print through fabric.
            t=max(0,min(1,(p.y-1.035)/.37))
            width=.183+.025*t
            depth=.130+.018*t
            angle=math.atan2((p.z+.006)/depth,p.x/width)
            mesh.vertices[local].co=v((width*math.cos(angle),p.y,-.006+depth*math.sin(angle)))
obj = bpy.data.objects.new('Athlete with fitted cricket kit', mesh)
bpy.context.collection.objects.link(obj)
assignments = {name: [] for name in bones}
for local, original in enumerate(used):
    merged={}
    for name, amount in weights[original].items():
        # The visible wrist skin follows the forearm; the closed glove alone follows the handle.
        # Otherwise a bat rotation rolls the forearm's last skin ring through the sleeve.
        if name.endswith('_hand'):
            name=name.replace('_hand','_forearm')
        merged[name]=merged.get(name,0)+amount
    for name,amount in merged.items():
        assignments[name].append((local,amount))
finish(obj, 'Athlete with fitted cricket kit', skin, assignments)
obj.data.materials.append(jersey)
obj.data.materials.append(trousers)
for polygon, source_face in zip(obj.data.polygons, faces):
    categories = [classes[i] for i in source_face]
    polygon.material_index = max(set(categories), key=categories.count)
surface=bmesh.new()
surface.from_mesh(obj.data)
bmesh.ops.remove_doubles(surface,verts=list(surface.verts),dist=.0015)
for height in [1.035,1.49]:
    bmesh.ops.bisect_plane(surface,geom=list(surface.verts)+list(surface.edges)+list(surface.faces),
        dist=.00001,plane_co=(0,0,height),plane_no=(0,0,1),clear_inner=False,clear_outer=False)
for face in surface.faces:
    centre=face.calc_center_median()
    if centre.z<1.035:
        face.material_index=2
    elif (centre.z<1.10 and abs(centre.x)<.25) or (1.445<centre.z<1.49 and abs(centre.x)<.10):
        face.material_index=1
    elif centre.z>1.49:
        face.material_index=0
bmesh.ops.recalc_face_normals(surface,faces=list(surface.faces))
surface.to_mesh(obj.data)
surface.free()
obj.data.update()

# A sewn polo opening and a narrow collar, following the chest/neck instead of rigid pipes.
tube('Polo placket', [(0,1.405,.151),(0,1.44,.135),(0,1.475,.106)], .004, navy, 'chest')
for side in [-1, 1]:
    mesh_part('Folded collar', [(side*.008,1.479,.106),(side*.052,1.505,.067),
        (side*.077,1.485,.091),(side*.035,1.45,.134)], [(0,1,2,3)], navy, 'chest')
for y in [1.42, 1.44]:
    ellipsoid('Polo button',(0,y,.145),(.003,.003,.002),leather,'chest',8,6)
# Thin contrast panels lie on the existing torso silhouette.
for sign in [-1,1]:
    tube('Waist piping', [(sign*.135,1.025,.055),(sign*.142,1.09,.065)], .0035, amber, 'pelvis')
    tube('Torso piping', [(sign*.142,1.115,.066),(sign*.16,1.26,.075)], .0035, amber, 'spine')

for side in ['front', 'rear']:
    knee, ankle = bones[side+'_shin']
    axis = (ankle-knee).normalized()
    forward = (Vector((0,0,1))-axis*axis.z).normalized()
    across = axis.cross(forward).normalized()
    # Curved shield: long stitched channels, an integrated horizontal knee roll, and no ball-shaped knee joint.
    points, shield_faces = [], []
    count_x, count_y = 20, 24
    for row in range(count_y+1):
        t = row/count_y
        centre = knee.lerp(ankle,t*.90) - axis*.055
        width = .092*(1-.28*t)
        knee_roll = .018*math.exp(-((t-.12)/.08)**2)
        for column in range(count_x+1):
            u = column/count_x*2-1
            quilting = .0045*(.5+.5*math.cos(u*math.pi*5))
            depth = .055+.026*math.sqrt(max(0,1-u*u))+quilting+knee_roll
            points.append(centre+across*(u*width)+forward*depth)
    for row in range(count_y):
        for col in range(count_x):
            a = row*(count_x+1)+col
            shield_faces.append((a,a+1,a+count_x+2,a+count_x+1))
    mesh_part(side+' shaped pad',points,shield_faces,leather,side+'_shin')
    for t in [.30,.76]:
        centre = knee.lerp(ankle,t)
        path = [centre + across*(math.cos(a)*.072) + forward*(math.sin(a)*.069)
                for a in [i*math.tau/24 for i in range(25)]]
        tube(side+' pad strap',path,.008,navy,side+'_shin')
    # Low-profile shoe upper with a flat sole and distinct heel, instep and toe.
    points, shoe_faces = [], []
    profiles=[(-.085,.035,.04),(-.065,.055,.08),(-.015,.058,.115),(.045,.06,.085),(.14,.052,.052),(.205,.027,.035)]
    for z,width,height in profiles:
        for j in range(16):
            a = j*math.tau/16
            points.append(ankle+Vector((width*math.cos(a),-.066+height*(math.sin(a)+1)*.5,z)))
    for row in range(len(profiles)-1):
        for j in range(16):
            shoe_faces.append((row*16+j,row*16+(j+1)%16,(row+1)*16+(j+1)%16,(row+1)*16+j))
    shoe_faces += [tuple(reversed(range(16))),tuple((len(profiles)-1)*16+j for j in range(16))]
    mesh_part(side+' shoe upper',points,shoe_faces,leather,side+'_foot')
    for z in [.015,.04,.065,.09]:
        tube(side+' laces',[ankle+Vector((-.038,.016,z)),ankle+Vector((.038,.016,z))],.0028,navy,side+'_foot')
    tube(side+' sole edge',[ankle+Vector((x,-.06,z)) for x,z in
         [(-.035,-.085),(-.06,-.015),(-.06,.08),(-.04,.18),(0,.21),(.04,.18),(.06,.08),(.06,-.015),(.035,-.085),(-.035,-.085)]],.008,navy,side+'_foot')

for side in ['top','bottom']:
    grip = bones[side+'_hand'][0]
    # Local Y follows the handle; local Z points toward the forearm. Four bent fingers wrap the handle.
    ellipsoid(side+' glove palm',grip+Vector((0,0,.027)),(.045,.051,.023),leather,side+'_hand')
    ellipsoid(side+' wrist cuff',grip+Vector((0,0,.062)),(.042,.043,.018),navy,side+'_hand')
    for finger in range(4):
        y = (finger-1.5)*.022
        path = [grip+Vector((x,y,z)) for x,z in [(-.026,.041),(-.040,.023),(-.038,-.010),(-.015,-.028),(.016,-.021)]]
        tube(side+' curved padded finger',path,.011,leather,side+'_hand',10)
        tube(side+' finger stitching',[grip+Vector((-.045,y-.006,.020)),grip+Vector((-.045,y+.006,.020))],.0018,seam,side+'_hand')
    tube(side+' opposing thumb',[grip+Vector((.036,-.04,.028)),grip+Vector((.041,-.012,.005)),
        grip+Vector((.022,.007,-.018))],.013,leather,side+'_hand',12)

head_base = bones['head'][0]
for eye in eyes:
    ellipsoid('Eye white',eye,(.012,.009,.009),leather,'head',16,10)
    ellipsoid('Brown iris',eye+Vector((0,0,.008)),(.005,.005,.002),rubber,'head',12,8)
# Helmet and grille follow the actual source skull. Forward is +Z before the head turns to the bowler.
points, helmet_faces = [], []
for row in range(13):
    theta=row/12*math.pi*.56
    for col in range(40):
        phi=col*math.tau/40
        points.append((.108*math.sin(theta)*math.cos(phi),1.735+.105*math.cos(theta),.027+.118*math.sin(theta)*math.sin(phi)))
for row in range(12):
    for col in range(40):
        helmet_faces.append((row*40+col,row*40+(col+1)%40,(row+1)*40+(col+1)%40,(row+1)*40+col))
mesh_part('Helmet shell',points,helmet_faces,navy,'head')
ellipsoid('Helmet peak',(0,1.727,.145),(.113,.010,.071),navy,'head',28,8)
for y in [1.58,1.622,1.664]:
    tube('Face grille',[(-.10,y,.055),(-.12,y,.145),(-.085,y,.192),(0,y,.214),(.085,y,.192),(.12,y,.145),(.10,y,.055)],.0028,steel,'head')
for sign in [-1,1]:
    tube('Grille support',[(sign*.12,1.58,.145),(sign*.115,1.73,.13)],.0035,steel,'head')
    ellipsoid('Ear guard',(sign*.102,1.665,.006),(.012,.052,.039),navy,'head',16,10)

# A flat willow blade with an edge bevel and a spine, rather than a round club.
grip = Vector((.65,1,.4))
bones['bat'] = (grip,grip+Vector((0,-.85,0)))
tube('Rubber bat handle',[grip+Vector((0,.05,0)),grip+Vector((0,-.29,0))],.017,rubber,'bat',12)
points, blade_faces = [], []
outline=[(-.031,-.26),(-.052,-.31),(-.055,-.77),(-.045,-.835),(0,-.85),(.045,-.835),(.055,-.77),(.052,-.31),(.031,-.26)]
for depth in [.016,-.025]:
    points.extend([grip+Vector((x,y,depth)) for x,y in outline])
n=len(outline)
blade_faces += [tuple(range(n)),tuple(reversed(range(n,2*n)))]
for i in range(n):
    blade_faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
blade=mesh_part('Flat willow blade',points,blade_faces,willow,'bat')
bevel=blade.modifiers.new('Rounded blade edges','BEVEL');bevel.width=.006;bevel.segments=3
bpy.context.view_layer.objects.active=blade
bpy.ops.object.modifier_apply(modifier=bevel.name)
mesh_part('Bat label',[grip+Vector((x,y,.018)) for x,y in [(-.039,-.34),(.039,-.34),(.039,-.46),(-.039,-.46)]],[(0,1,2,3)],navy,'bat')

bpy.ops.object.select_all(action='DESELECT')
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
body=bpy.context.object
body.name='AthleteMesh'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
armature=bpy.data.armatures.new('AthleteSkeleton')
rig=bpy.data.objects.new('Cricketer',armature)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for name,(a,b) in bones.items():
    bone=armature.edit_bones.new(name)
    bone.head,bone.tail=v(a),v(b)
    bone.align_roll(v((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')
modifier=body.modifiers.new('Continuous human skin','ARMATURE')
modifier.object=rig
body.parent=rig
rig.show_in_front=True
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True);rig.select_set(True)
bpy.context.view_layer.objects.active=rig
OUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.export_scene.gltf(filepath=str(OUT/'batsman.glb'),export_format='GLB',use_selection=True,export_animations=False,export_yup=True)
metadata={'Version':2,'Names':list(bones),'Lengths':[(b-a).length for a,b in bones.values()]}
(OUT/'batsman-rig.json').write_text(json.dumps(metadata,indent=2))
motion_path=SOURCE/'reference-motion.json'
if motion_path.exists():
    motion=json.loads(motion_path.read_text())
    if motion.get('RigVersion')==2:
        bpy.context.scene.render.fps=motion['Fps']
        conversion=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
        rig.animation_data_create()
        for clip,frames in motion['Clips'].items():
            action=bpy.data.actions.new(clip);action.use_fake_user=True
            rig.animation_data.action=action
            for frame_index,transforms in enumerate(frames,1):
                for name,t in zip(bones,transforms):
                    transform=Matrix(((t[0],t[3],t[6],t[9]),(t[1],t[4],t[7],t[10]),(t[2],t[5],t[8],t[11]),(0,0,0,1)))
                    pose=rig.pose.bones[name];pose.rotation_mode='QUATERNION'
                    pose.matrix=conversion@transform@conversion.inverted()
                    for channel in ['location','rotation_quaternion','scale']:
                        pose.keyframe_insert(channel,frame=frame_index,group=name)
        rig.animation_data.action=bpy.data.actions['Fast']
        bpy.context.scene.frame_end=len(motion['Clips']['Fast'])
        bpy.context.scene.frame_set(1)
# Review studio is saved only in the editable source, after the runtime GLB export.
bpy.ops.object.camera_add(location=(0,-6,1.4))
camera=bpy.context.object
camera.name='Batsman review camera'
camera.rotation_euler=(Vector((0,0,1.4))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'
camera.data.ortho_scale=3.2
bpy.context.scene.camera=camera
for name,position,energy,size in [('Key',(-3,-4,5),450,4),('Fill',(3,-2,3),180,3),('Rim',(0,3,4),300,3)]:
    bpy.ops.object.light_add(type='AREA',location=position)
    lamp=bpy.context.object
    lamp.name=name
    lamp.data.energy=energy
    lamp.data.shape='DISK'
    lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,1.1))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=scene.render.resolution_y=1024
scene.render.resolution_percentage=100
scene.render.film_transparent=True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'batsman.blend'))
report={'vertices':len(body.data.vertices),'polygons':len(body.data.polygons),'bones':len(bones),
        'materials':len(set(m.name for m in body.data.materials)),
        'source_actions':{a.name:list(a.frame_range) for a in bpy.data.actions}}
(OUT/'batsman-build.json').write_text(json.dumps(report,indent=2))
print('BATSMAN_BUILD_COMPLETE',report)
