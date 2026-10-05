"""Review-only bow sonar block; authoring +Y bow, +Z up, floor Z=0.

The exposed acoustic nose makes the game's forward-facing placement legible.
It is a stylised equipment block, not a full-size, below-waterline hull dome.
No Unity files or scene state are modified by this module.
"""
import math

import environment_detail_common as c


def _closed_vertical_profile(name, outline, bottom, top, root, col, material):
    """Extrude a horizontal polygon into one closed solid."""
    count = len(outline)
    verts = [(x, y, z) for z in (bottom, top) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
              for i in range(count)]
    return c.mesh(name, verts, faces, root, col, material)


def _equipment_shell(root, col):
    # Chamfered rear-high, front-low watertight casing: an explicit bow direction.
    profile = [(-.69, .14), (.59, .14), (.67, .27), (.60, .51),
               (.38, .62), (-.52, .79), (-.69, .67)]
    verts = [(x, y, z) for x in (-.68, .68) for y, z in profile]
    n = len(profile)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return c.mesh('BowWatertightWedgeHousing', verts, faces, root, col,
                  'Naval Blue Grey')


def build_bow_sonar(root, col):
    """Create a static hull-sonar inspired module, without export or scene setup."""
    c.box('BowSonarDeckFoot', (0, 0, .05), (1.74, 1.72, .10), root, col,
          'Deck Grey')
    c.box('BowSonarIsolationPlinth', (0, -.02, .12), (1.46, 1.43, .10), root,
          col, 'Gunmetal')
    _equipment_shell(root, col)

    # The arc is forward-bulging, with no rear acoustic window. Every piece is
    # closed; no material transparency is needed to suggest rubber/ceramic.
    arc = []
    for i in range(11):
        theta = -math.pi / 2 + math.pi * i / 10
        arc.append((.68 * math.sin(theta), .58 + .24 * math.cos(theta)))
    outline = arc + [(.68, .515), (-.68, .515)]
    panel = _closed_vertical_profile('ForwardCurvedAcousticWindow', outline,
                                    .18, .53, root, col, 'Sensor Glass')
    # Smooth only the vertically extruded acoustic face; top/bottom stay flat.
    for face in panel.data.polygons[2:12]:
        face.use_smooth = True
    for i, (start, end) in enumerate(zip(arc[:-1], arc[1:]), 1):
        c.beam('AcousticUpperSeal_%02d' % i, (*start, .535), (*end, .535),
               .036, root, col, 'Gunmetal')
        c.beam('AcousticLowerSeal_%02d' % i, (*start, .185), (*end, .185),
               .032, root, col, 'Gunmetal')

    # Low aft equipment enclosure, attached to the casing rather than floating.
    c.box('AftSealedElectronicsCover', (0, -.47, .705), (1.19, .28, .20),
          root, col, 'Naval Blue Grey')
    c.box('AftCoverGasket', (0, -.47, .712), (1.23, .305, .025), root, col,
          'Gunmetal')
    c.box('AftSealedElectronicsTop', (0, -.47, .807), (1.19, .28, .022),
          root, col, 'Naval Blue Grey')
    # Maintenance door and rear gland have obvious service functions.
    c.box('PortMaintenanceDoor', (-.688, -.19, .45), (.023, .59, .29), root,
          col, 'Deck Grey')
    c.box('PortMaintenanceHandle', (-.713, -.21, .46), (.036, .15, .037),
          root, col, 'Gunmetal')
    c.cyl('AftCableGland', (.37, -.714, .36), .074, .08, root, col,
          'Gunmetal', segments=10, rot=(math.pi / 2, 0, 0), smooth=False)
    c.beam('ProtectedCableDrop', (.37, -.75, .36), (.37, -.77, .16), .06,
           root, col, 'Gunmetal')
    c.beam('ProtectedCableDeckRun', (.37, -.77, .16), (0, -.77, .16), .06,
           root, col, 'Gunmetal')
    # Four manufactured fasteners secure the foot. Yellow nose tabs express
    # placement orientation without introducing another fictional sensor.
    for i, (x, y) in enumerate(((-.77, -.73), (.77, -.73),
                               (-.77, .71), (.77, .71)), 1):
        c.cyl('DeckAnchorBolt_%02d' % i, (x, y, .115), .033, .03, root,
              col, 'Gunmetal', segments=6, smooth=False)
    for side, x in (('Port', -.79), ('Starboard', .79)):
        c.box('ForwardPlacementTab_' + side, (x, .59, .11),
              (.074, .27, .025), root, col, 'Warning Yellow')
    c.box('SonarServiceIdentification', (-.699, -.39, .60),
          (.025, .15, .047), root, col, 'Medical White')

    c.empty('SonarOrigin', root, col, (0, .824, .355))
    c.empty('ForwardMarker', root, col, (0, .86, .355))
    c.empty('AttachPoint', root, col, (0, 0, 0))
    root['module_role'] = 'bow_hull_sonar'
    root['game_design_sector_degrees'] = 140
    root['game_design_sector_note'] = (
        '+/-70 degrees is a gameplay rule, not a real hull sonar limitation.')
    root['simplification'] = (
        'Deck-visible 1x1 game equipment representation: no below-floor dome; '
        'actual hull-mounted transducers usually operate below the waterline.')
    root['moving_parts'] = 'none'
    return {
        'description': '선수 방향을 식별할 수 있는 낮은 쐐기형 케이싱과 곡면 음향창의 선수 소나 블록.',
        'features': ['전방 곡면 음향창', '뒤가 높은 밀폐 쐐기형 케이싱',
                     '후방 밀폐 전자장비 덮개·케이블 글랜드',
                     '측면 정비 도어', '고정식 구조·회전부 없음'],
        'reference_urls': [
            'https://www.kongsberg.com/what-we-do/defence-and-security/naval/ss2030/'
        ],
        'budget': 900,
        'required_sockets': ['SonarOrigin', 'ForwardMarker', 'AttachPoint'],
        'gameplay_not_implemented': True,
        'realism_note': ('SS2030 등 실제 선체 소나는 360도 탐지가 가능하다. '
                         '전방 ±70도는 사용자 게임 설계이며, 모델은 수면 아래 실제 '
                         '소나 돔을 갑판 위 블록으로 시각적으로 단순화한 것이다.'),
    }
