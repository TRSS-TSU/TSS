from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


OUTPUT = r"C:\Unity\TSS2\Docs\Plans\2026-10-14 TSS2 Wireless Capstone Morning Brief.docx"


def shade(cell, fill):
    properties = cell._tc.get_or_add_tcPr()
    shading = OxmlElement("w:shd")
    shading.set(qn("w:fill"), fill)
    properties.append(shading)


def borders(cell):
    properties = cell._tc.get_or_add_tcPr()
    element = properties.first_child_found_in("w:tcBorders")
    if element is None:
        element = OxmlElement("w:tcBorders")
        properties.append(element)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        tag = element.find(qn(f"w:{edge}"))
        if tag is None:
            tag = OxmlElement(f"w:{edge}")
            element.append(tag)
        tag.set(qn("w:val"), "single")
        tag.set(qn("w:sz"), "4")
        tag.set(qn("w:color"), "D9D9D9")


def cell_text(cell, text, bold=False, color=None, size=9.5):
    paragraph = cell.paragraphs[0]
    paragraph.paragraph_format.space_after = Pt(2)
    run = paragraph.add_run(text)
    run.bold = bold
    run.font.name = "Aptos"
    run._element.rPr.rFonts.set(qn("w:ascii"), "Aptos")
    run._element.rPr.rFonts.set(qn("w:hAnsi"), "Aptos")
    run.font.size = Pt(size)
    if color:
        run.font.color.rgb = RGBColor(*color)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    borders(cell)


def add_bullet(document, text):
    paragraph = document.add_paragraph(style="List Bullet")
    paragraph.paragraph_format.space_after = Pt(3)
    paragraph.add_run(text)
    return paragraph


def add_heading(document, text):
    paragraph = document.add_heading(text, level=1)
    paragraph.paragraph_format.space_before = Pt(12)
    paragraph.paragraph_format.space_after = Pt(4)
    paragraph.paragraph_format.keep_with_next = True
    return paragraph


document = Document()
section = document.sections[0]
section.top_margin = Inches(0.68)
section.bottom_margin = Inches(0.68)
section.left_margin = Inches(0.72)
section.right_margin = Inches(0.72)

normal = document.styles["Normal"]
normal.font.name = "Aptos"
normal._element.rPr.rFonts.set(qn("w:ascii"), "Aptos")
normal._element.rPr.rFonts.set(qn("w:hAnsi"), "Aptos")
normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(5)

for style_name, size in (("Title", 22), ("Heading 1", 14), ("Heading 2", 11)):
    style = document.styles[style_name]
    style.font.name = "Aptos Display" if style_name == "Title" else "Aptos"
    style._element.rPr.rFonts.set(qn("w:ascii"), style.font.name)
    style._element.rPr.rFonts.set(qn("w:hAnsi"), style.font.name)
    style.font.size = Pt(size)
    style.font.color.rgb = RGBColor(0, 0, 0)

title = document.add_paragraph(style="Title")
title.alignment = WD_ALIGN_PARAGRAPH.LEFT
title.add_run("TSS2 Wireless Capstone Morning Brief")

subtitle = document.add_paragraph()
subtitle.paragraph_format.space_after = Pt(10)
run = subtitle.add_run("Developer handoff | October 14, 2026 | Unity project C:\\Unity\\TSS2")
run.italic = True
run.font.size = Pt(10)
run.font.color.rgb = RGBColor(70, 70, 70)

document.add_paragraph(
    "The wireless-capstone placement, presentation, interaction, and cable-route foundation is in place. "
    "The next developer should visually tune the two Reception-wall adjustment nodes and choose the numbered wall-port policy before making the AP connection a scored capstone requirement."
)

add_heading(document, "Current state")
for item in (
    "Equipment support: AccessPoint and Laptop are valid placement categories at LobbyTable01. The scenario includes one of each.",
    "Access point: AccessPoint_UAP_nanoHD prefab has a bindable EthernetPort marker and a selectable Ethernet collider. Its LED ring follows live physical connectivity.",
    "Laptop: Laptop 1 prefab has an interactive first-person view, state-driven screen image, dedicated laptop session canvas, and dedicated first-person controls prefab.",
    "Session behavior: TssWirelessDevicePresentation refreshes from TssTrainingSession.StateChanged. An AP cable connection lights the AP LED and changes the laptop screen to the online image.",
):
    add_bullet(document, item)

add_heading(document, "Placement and cable route")
table = document.add_table(rows=1, cols=3)
table.alignment = WD_TABLE_ALIGNMENT.CENTER
table.style = "Table Grid"
headers = ("Stage", "World position", "Purpose")
for cell, text in zip(table.rows[0].cells, headers):
    shade(cell, "1F4E78")
    cell_text(cell, text, bold=True, color=(255, 255, 255), size=9.5)

rows = (
    ("LobbyTable AP anchor", "12.70, 3.12, -9.05", "AP mount and cable entry"),
    ("CeilingExit", "12.70, 3.12, -9.05", "Station-bound entry; stationId must remain LobbyTable01"),
    ("CeilingRun", "12.70, 3.10, -10.93", "Adjustable ceiling run"),
    ("ReceptionWall_Ceiling", "9.72, 3.10, -10.93", "Adjustable ceiling-to-wall handoff"),
    ("ReceptionWall_PortRiser", "9.66, 0.65, -10.88", "Adjustable vertical approach to the wall-port block"),
)
for index, row in enumerate(rows):
    cells = table.add_row().cells
    if index % 2:
        for cell in cells:
            shade(cell, "F3F7FA")
    for cell, text in zip(cells, row):
        cell_text(cell, text, bold=(cell is cells[0]))

document.add_paragraph(
    "The authored route is CeilingExit -> CeilingRun -> ReceptionWall_Ceiling -> ReceptionWall_PortRiser -> Reception path. "
    "The two Reception-wall nodes are intentionally simple adjustment points so the cable can be tuned against the ceiling, wall, and port geometry without changing routing code."
)

add_heading(document, "Key files")
files = document.add_table(rows=1, cols=2)
files.alignment = WD_TABLE_ALIGNMENT.CENTER
files.style = "Table Grid"
for cell, text in zip(files.rows[0].cells, ("Asset or script", "Role")):
    shade(cell, "1F4E78")
    cell_text(cell, text, bold=True, color=(255, 255, 255), size=9.5)
for index, row in enumerate((
    ("Assets/Scenes/TSS.unity", "LobbyTable anchors, device-session bindings, and AP cable-route nodes"),
    ("Assets/Generated/Blender/AccessPoint/AccessPoint_UAP_nanoHD.prefab", "AP visual, LED-ring target, EthernetPort marker, and Ethernet collider"),
    ("Assets/Laptop/prefab/Laptop 1.prefab", "Laptop screen target and first-person interaction"),
    ("Assets/Scripts/TssWirelessDevicePresentation.cs", "Session-state to LED and screen presentation adapter"),
    ("Assets/Prefabs/UI/TssLaptopSessionCanvas.prefab", "Laptop configuration and diagnostics session"),
    ("Assets/Tests/EditMode/TssWirelessLobbyTableRouteTests.cs", "Minimal route-selection regression check"),
)):
    cells = files.add_row().cells
    if index % 2:
        for cell in cells:
            shade(cell, "F3F7FA")
    for cell, text in zip(cells, row):
        cell_text(cell, text, size=9.2)

add_heading(document, "Verification completed")
for item in (
    "Unity compilation completed without errors after the presentation, UI, and routing additions.",
    "Play Mode opened without console errors after the wireless changes.",
    "The LobbyTable AP-to-Reception route was verified using all four named adjustment nodes; the current path contains seven route points.",
    "TssWirelessLobbyTableRouteTests passed as an edit-mode check for the LobbyTable01 station route.",
):
    add_bullet(document, item)

add_heading(document, "Next developer actions")
for item in (
    "In Scene view, move ReceptionWall_Ceiling and ReceptionWall_PortRiser to match the final ceiling penetration, wall path, and selected jack. Keep CeilingExit stationId as LobbyTable01.",
    "Choose the specific Reception wall-port number. Ports 01 through 04 already have permanent infrastructure assignments; do not repurpose a port or change its endpoint ID or display label without an explicit scenario decision.",
    "If the capstone must use Rack01 patch-panel Port17 and AN-1 Fa0/7, add that permanent infrastructure path deliberately and then extend the evaluator. The current physical route supports player cabling but does not hard-code a scored AP wall-port assignment.",
    "Perform a final in-game pass: place AP and laptop, connect the AP Ethernet port to the chosen Reception wall port, verify LED and laptop screen transitions, then open the laptop session UI and confirm the return path.",
):
    add_bullet(document, item)

document.save(OUTPUT)
print(OUTPUT)
