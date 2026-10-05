"""Review-only inboard sonar block: protected circular hull unit and operator console."""
import math
from mathutils import Vector, Euler
import environment_detail_common as c


def build_internal_sonar(root, col):
    grey, deck, dark, face = 'Naval Blue Grey', 'Deck Grey', 'Gunmetal', 'Sensor Glass'
    white, yellow = 'Medical White', 'Warning Yellow'
    c.box('InboardEquipmentFoundation', (0, 0, .05), (1.76, 1.76, .10), root, col, deck)

    # One mechanically supported instrument cabinet, not an open decorative cage.
    c.frustum('AcousticProcessorCabinet', (-.42, -.12, .325),
              (.65, .80), (.61, .72), .45, root, col, grey)
    c.box('CabinetServiceDoor', (-.42, .281, .323), (.52, .015, .34), root, col, deck)
    c.box('CabinetDoorLatch', (-.24, .297, .324), (.034, .026, .094), root, col, dark)
    for i in range(4):
        c.box('ProcessorCoolingSlot_%02d' % (i+1), (-.749, -.12, .235+i*.058),
              (.015, .36, .018), root, col, dark)

    angle = math.radians(-17)
    rotation = Euler((angle, 0, 0)).to_matrix()
    origin = Vector((-.42, .16, .65))

    def screen_part(name, offset, dims, mat):
        return c.box(name, origin+rotation@Vector(offset), dims, root, col, mat, rot=(angle, 0, 0))

    # Front-facing inclined console with a genuinely legible circular sonar scope.
    screen_part('InclinedSonarConsole', (0, 0, 0), (.66, .095, .34), grey)
    screen_part('ConsoleBlackBezel', (0, .052, .012), (.58, .025, .255), dark)
    screen_part('ConsoleScopeFace', (0, .067, .012), (.52, .012, .208), face)
    scope_origin = origin+rotation@Vector((-.105, .078, .01))
    c.ring('SonarScopeRangeRing', scope_origin, .077, .006,
           root, col, white, segments=12, cross=3, rot=(math.pi/2+angle, 0, 0))
    c.beam('ScopeSweepLine', scope_origin, scope_origin+rotation@Vector((.045, 0, .055)),
           .010, root, col, white)
    for i, width in enumerate((.12, .085, .105)):
        screen_part('ContactReadout_%02d' % (i+1), (.135, .077, .060-i*.047),
                    (width, .007, .012), white)
    screen_part('ProtectedControlShelf', (0, .098, -.173), (.63, .19, .049), deck)
    for i in range(3):
        screen_part('ConsoleControl_%02d' % (i+1), (-.16+i*.16, .165, -.140),
                    (.058, .035, .016), yellow if i == 0 else dark)

    # A low circular hull-unit crown is exposed as a schematic block top.
    # The actual water-coupled transducer would sit below the hull; this is a
    # readable miniature/cutaway interpretation, not an above-water sonar claim.
    unit = c.empty('TransducerAssembly', root, col, (.38, -.09, 0))
    c.cyl('HullUnitBaseFlange', (0, 0, .14), .352, .08, unit, col, deck, segments=12)
    c.lathe('ProtectedCircularHullUnit', (0, 0, 0),
            [(.18, .294), (.22, .316), (.48, .316), (.55, .270), (.575, .245)],
            unit, col, grey, segments=12, smooth=False)
    c.cyl('AcousticArrayBand', (0, 0, .325), .319, .112, unit, col, face, segments=12, smooth=False)
    for i in range(8):
        a = i*math.tau/8
        c.box('AcousticBandSeparator_%02d' % (i+1), (.319*math.cos(a), .319*math.sin(a), .325),
              (.012, .053, .118), unit, col, grey, rot=(0, 0, a))
    c.ring('HullUnitSeal', (0, 0, .523), .276, .014,
           unit, col, dark, segments=12, cross=3)
    c.cyl('ServiceAccessCover', (0, 0, .580), .180, .026, unit, col, deck, segments=12)
    c.box('ServiceAccessHandle', (0, 0, .604), (.20, .042, .028), unit, col, grey)

    # One supported harness links the circular unit to its electronics cabinet.
    for i, (start, end) in enumerate([
            ((.13, -.20, .20), (.02, -.37, .18)),
            ((.02, -.37, .18), (-.12, -.43, .18)),
            ((-.12, -.43, .18), (-.28, -.43, .21))], 1):
        c.beam('ProcessorHarness_%02d' % i, start, end, .035, root, col, dark)
    c.box('HarnessClamp', (-.06, -.40, .158), (.07, .10, .048), root, col, grey)
    c.box('InboardIdentificationPlate', (.59, .66, .109), (.25, .16, .018), root, col, white)
    c.box('IdentificationCentreMark', (.59, .66, .121), (.13, .035, .008), root, col, face)

    c.empty('SonarOrigin', root, col, (.38, -.09, .325))
    c.empty('ForwardMarker', root, col, (0, .84, .12))
    c.empty('AttachPoint', root, col)
    c.empty('OperatorPanel', root, col, tuple(origin))
    return {
        'description': 'Inboard compact sonar hull unit and acoustic-processing console',
        'features': ['Low sealed circular hull-unit crown', 'Protected processor cabinet',
                     'Inclined sonar scope', 'Connected processor harness',
                     'Cutaway-style readable inboard equipment'],
        'budget': 900,
        'required_sockets': ['SonarOrigin', 'ForwardMarker', 'AttachPoint', 'OperatorPanel', 'TransducerAssembly'],
        'reference_urls': ['https://www.kongsberg.com/what-we-do/defence-and-security/naval/ss2030/'],
        'reference_fidelity': 'Miniature/cutaway interpretation of hull unit plus processor/operator panel; the real transducer couples to water below the hull.',
        'gameplay_note': 'The short omnidirectional range is the supplied game rule, not a published hardware specification.',
    }
