from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

OUT = r"C:\Unity\TSS2\Naming_Standard_Tab_Review.docx"

def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:fill'), fill)
    tc_pr.append(shd)

def set_cell(cell, text, bold=False, color=(0, 0, 0)):
    cell.text = ''
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(0)
    r = p.add_run(text)
    r.bold = bold
    r.font.color.rgb = RGBColor(*color)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER

def add_table(doc, title, headers, rows):
    doc.add_heading(title, level=1)
    table = doc.add_table(rows=1, cols=len(headers))
    table.style = 'Table Grid'
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    for cell, header in zip(table.rows[0].cells, headers):
        set_cell(cell, header, bold=True, color=(255, 255, 255))
        shade(cell, '1F4E78')
    for idx, row in enumerate(rows):
        cells = table.add_row().cells
        for cell, value in zip(cells, row):
            set_cell(cell, value, bold=(row[0] == 'U1 (top)' or row[0] == 'U1'))
        if idx % 2 == 1:
            for cell in cells:
                shade(cell, 'EAF2F8')
    doc.add_paragraph()

doc = Document()
section = doc.sections[0]
section.top_margin = Inches(0.7)
section.bottom_margin = Inches(0.7)
section.left_margin = Inches(0.8)
section.right_margin = Inches(0.8)

normal = doc.styles['Normal']
normal.font.name = 'Aptos'
normal._element.rPr.rFonts.set(qn('w:eastAsia'), 'Aptos')
normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.08
for style_name, size in [('Title', 22), ('Heading 1', 15)]:
    style = doc.styles[style_name]
    style.font.name = 'Aptos'
    style._element.rPr.rFonts.set(qn('w:eastAsia'), 'Aptos')
    style.font.size = Pt(size)
    style.font.color.rgb = RGBColor(0, 0, 0)

title = doc.add_paragraph(style='Title')
title.add_run('Naming Standard')
subtitle = doc.add_paragraph('Technical Order 1042', style='Subtitle')
subtitle.runs[0].font.color.rgb = RGBColor(89, 89, 89)

doc.add_heading('Rack Naming and Mounting Standard', level=1)
doc.add_paragraph('AN = Access Node (switch). CN = Core Node (router). Rack order is not cosmetic: power at the top, away from data; patch panel directly above the switch it feeds; router beside the switch it uplinks to; and the heavy box lowest for center of gravity.')

add_table(doc, 'First Floor Rack', ['Slot', 'Device', 'Name', 'Why'], [
    ('U1 (top)', 'Rack PDU', 'PDU1', 'Power at the top, away from data cabling'),
    ('U2', '8-port patch panel', 'Patch1', 'Directly above its switch – short patch runs'),
    ('U3', '8-port switch', 'AN-1', 'Access node, ground floor'),
    ('U4', 'Core router', 'CN-1', 'Beside the switch it trunks to; feeds the demarc'),
    ('U5', 'Rack server', 'IT2', 'Heavy and hot – lowest in the rack'),
    ('U6', '—', 'spare', 'Leave open for growth'),
])

add_table(doc, 'Second Floor Rack', ['Slot', 'Device', 'Name'], [
    ('U1', 'Rack PDU', 'PDU2'),
    ('U2', 'Patch panel', 'Patch2'),
    ('U3', 'Switch', 'AN-2'),
])

p = doc.add_paragraph()
p.paragraph_format.left_indent = Inches(0.15)
r = p.add_run('No server upstairs. Wall mounts are already installed — facilities did that.')
r.italic = True
r.font.color.rgb = RGBColor(89, 89, 89)

doc.save(OUT)
print(OUT)
