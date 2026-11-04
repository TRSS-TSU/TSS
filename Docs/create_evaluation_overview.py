from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.section import WD_SECTION
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.style import WD_STYLE_TYPE
from pathlib import Path

OUT = Path(r"C:\Unity\TSS2\Docs\Customer_Evaluation_Overview.docx")

def shade(cell, fill):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = tcPr.find(qn('w:shd'))
    if shd is None:
        shd = OxmlElement('w:shd'); tcPr.append(shd)
    shd.set(qn('w:fill'), fill)

def borders(table, color='D9D9D9'):
    tblPr = table._tbl.tblPr
    b = tblPr.first_child_found_in('w:tblBorders')
    if b is None:
        b = OxmlElement('w:tblBorders'); tblPr.append(b)
    for edge in ('top','left','bottom','right','insideH','insideV'):
        tag = 'w:' + edge
        el = b.find(qn(tag))
        if el is None:
            el = OxmlElement(tag); b.append(el)
        el.set(qn('w:val'), 'single'); el.set(qn('w:sz'), '6'); el.set(qn('w:space'), '0'); el.set(qn('w:color'), color)

def set_cell(cell, text, bold=False, color='000000', size=9.5):
    cell.text = ''
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(2)
    run = p.add_run(text)
    run.bold = bold; run.font.size = Pt(size); run.font.color.rgb = RGBColor.from_string(color)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER

def add_table(doc, headers, rows, widths=None):
    table = doc.add_table(rows=1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.style = 'Table Grid'
    borders(table)
    for i, h in enumerate(headers):
        set_cell(table.rows[0].cells[i], h, True, 'FFFFFF', 9)
        shade(table.rows[0].cells[i], '17365D')
    for ri, row in enumerate(rows):
        cells = table.add_row().cells
        for i, value in enumerate(row):
            set_cell(cells[i], value, False, '000000', 9)
            if ri % 2 == 1: shade(cells[i], 'F2F6FA')
    if widths:
        for row in table.rows:
            for i, width in enumerate(widths): row.cells[i].width = Inches(width)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return table

def bullet(doc, text, level=0):
    p = doc.add_paragraph(style='List Bullet' if level == 0 else 'List Bullet 2')
    p.paragraph_format.space_after = Pt(2)
    p.add_run(text)
    return p

doc = Document()
sec = doc.sections[0]
sec.top_margin = Inches(.65); sec.bottom_margin = Inches(.65); sec.left_margin = Inches(.75); sec.right_margin = Inches(.75)

styles = doc.styles
styles['Normal'].font.name = 'Aptos'; styles['Normal'].font.size = Pt(10); styles['Normal'].font.color.rgb = RGBColor(0,0,0)
styles['Normal']._element.rPr.rFonts.set(qn('w:eastAsia'), 'Aptos')
for name, size, color in [('Title', 24, '000000'), ('Heading 1', 16, '000000'), ('Heading 2', 12, '17365D')]:
    st = styles[name]; st.font.name = 'Aptos Display' if name == 'Title' else 'Aptos'; st.font.size = Pt(size); st.font.bold = True; st.font.color.rgb = RGBColor.from_string(color)
    st._element.rPr.rFonts.set(qn('w:eastAsia'), st.font.name)
styles['Heading 1'].paragraph_format.space_before = Pt(14); styles['Heading 1'].paragraph_format.space_after = Pt(6)
styles['Heading 2'].paragraph_format.space_before = Pt(9); styles['Heading 2'].paragraph_format.space_after = Pt(4)

p = doc.add_paragraph(style='Title'); p.alignment = WD_ALIGN_PARAGRAPH.LEFT; p.add_run('Currently Tracked Evaluation Items')
p = doc.add_paragraph(); p.paragraph_format.space_after = Pt(12)
r = p.add_run('Customer overview of work package evaluation and Results review'); r.bold = True; r.font.size = Pt(12); r.font.color.rgb = RGBColor.from_string('17365D')

doc.add_paragraph('Purpose and scope', style='Heading 1')
doc.add_paragraph('This overview summarizes the evaluation items currently tracked by the simulator at scenario exit. It is grouped by the four work packages shown in the Results scene and reflects the evaluation code as implemented. The goal is to give the customer a clear view of what is being assessed, what evidence is used, and how the result is presented to the trainee or reviewer.')
doc.add_paragraph('The current assessment is a live-state exit assessment. A work package passes when every criterion produced for that package is complete. The Results scene separates verified criteria from actionable remaining criteria and also provides a device-configuration review for configurable network devices.')

doc.add_paragraph('At a glance', style='Heading 1')
add_table(doc, ['Work package', 'Focus', 'Tracked result'], [
    ('WP1', 'Physical Installation', 'Placement, required cable types, permanent connections, and terminations when required.'),
    ('WP2', 'Layer 2 Data Link', 'Switch hardening, management SVI, VLANs, access ports, trunks, unused ports, verification, and saved configuration.'),
    ('WP3', 'Layer 3', 'Router interfaces, WAN, routing evidence, default route, endpoint evidence, and saved configuration.'),
    ('WP4', 'Wireless', 'AP uplink, AP switchport, AP wireless settings, laptop association, DHCP, diagnostics, and RedWolf evidence.'),
], [1.0, 1.65, 4.65])

doc.add_paragraph('Work package details', style='Heading 1')

doc.add_paragraph('WP1 Physical Installation', style='Heading 2')
doc.add_paragraph('WP1 is driven by the scenario’s physical requirements and the current training-session state. Its criteria are data-driven, so the exact number of rows can change with the scenario configuration.')
add_table(doc, ['Tracked item', 'What is checked'], [
    ('Equipment placement', 'Each required rack or endpoint item is installed at the required location.'),
    ('Required cable types', 'Each scenario cable requirement is connected and uses the required cable type.'),
    ('Permanent connections', 'Each required permanent connection is installed.'),
    ('Punch-down terminations', 'For permanent connections that require termination, both required terminations are correct.'),
    ('Requirement availability', 'If the scenario has no physical requirements, the evaluator records that no physical installation requirements are defined.'),
], [1.85, 5.45])

doc.add_paragraph('WP2 Layer 2 Data Link', style='Heading 2')
doc.add_paragraph('WP2 evaluates both expected switches, identified in the current model as AN-1 and AN-2. Several checks are repeated per device or per required port, so the final criterion count depends on the network profile and scenario port requirements.')
add_table(doc, ['Tracked item', 'What is checked'], [
    ('Device hardening', 'Required passwords, secret, login requirements, password encryption, domain lookup behavior, MOTD, and expected hostname.'),
    ('Management SVI', 'VLAN 1 management address and mask, including the SVI administrative state.'),
    ('VLAN database', 'Required management, reception, data, native, and dead VLANs; no unexpected VLANs; dead VLAN is suspended as expected.'),
    ('Access ports', 'Scenario-required access ports use the correct VLAN, are not trunks, and are operational.'),
    ('Trunk ports', 'AN-1 Fa0/1, AN-1 Fa0/6, and AN-2 Fa0/1 use trunking, the required native VLAN, and the required allowed VLAN set.'),
    ('Unused ports', 'Unused port ranges are assigned to the dead VLAN and administratively shut.'),
    ('Verification evidence', 'The trainee has entered both `show vlan brief` and `show interfaces trunk` evidence for each switch.'),
    ('Saved configuration', 'The device has save evidence and its current state matches the startup snapshot.'),
], [1.85, 5.45])

doc.add_paragraph('WP3 Layer 3', style='Heading 2')
doc.add_paragraph('WP3 evaluates the expected router, currently identified as CN-1, and combines configuration state with evidence captured from the router and endpoints.')
add_table(doc, ['Tracked item', 'What is checked'], [
    ('Router availability', 'The expected router configuration is available for evaluation.'),
    ('Router hardening', 'Required security and hostname settings are complete.'),
    ('Parent interface', 'The router parent interface is operational and configured as a non-trunk interface.'),
    ('Subinterfaces', 'The management, reception, and data VLAN subinterfaces have the expected 802.1Q VLAN IDs, addresses, masks, and operational state.'),
    ('Provider-facing WAN', 'The WAN interface is operational, non-trunk, and uses the expected customer address and /30 mask.'),
    ('Route verification evidence', 'Required router route verification has been entered through `show ip route` evidence.'),
    ('Default route', 'A 0.0.0.0/0 route points to the configured provider address.'),
    ('Endpoint evidence', 'Every scenario-defined endpoint diagnostic requirement is present in the endpoint and session evidence.'),
    ('Saved configuration', 'The router has save evidence and matches its startup snapshot.'),
], [1.85, 5.45])

doc.add_paragraph('WP4 Wireless', style='Heading 2')
doc.add_paragraph('WP4 evaluates the access point and the scenario test laptop. If the laptop is unavailable, the evaluator creates four actionable remaining criteria so the missing training evidence is explicit.')
add_table(doc, ['Tracked item', 'What is checked'], [
    ('AP uplink', 'The access-point uplink is connected.'),
    ('AP switchport', 'The switchport used by the AP is ready.'),
    ('AP wireless configuration', 'The AP wireless settings are configured.'),
    ('Laptop association', 'The test laptop is associated to the guest AP and has Layer 1 and Layer 2 connectivity.'),
    ('DHCP lease', 'The laptop uses DHCP and has the expected test-laptop address.'),
    ('Laptop diagnostics', 'The laptop contains both `ipconfig` evidence and the provider-diagnostic evidence.'),
    ('RedWolf evidence', 'The required RedWolf page evidence is present.'),
], [1.85, 5.45])

doc.add_paragraph('How the Results scene presents evaluation', style='Heading 1')
doc.add_paragraph('The Results scene creates a four-page work-package review. Each page uses the work-package title, a PASS or INCOMPLETE status, a VERIFIED AT EXIT list, and an ACTIONABLE REMAINING CRITERIA list when applicable. The overall summary reports COMPLETE only when all four work packages pass; otherwise it reports INCOMPLETE and directs the reviewer to the remaining criteria.')
bullet(doc, 'Work packages are shown as WP1 Physical Installation, WP2 Layer 2 Data Link, WP3 Layer 3, and WP4 Wireless.')
bullet(doc, 'Verified criteria are shown with [PASS] markers.')
bullet(doc, 'Incomplete criteria are shown as actionable checklist items.')
bullet(doc, 'The reviewer can move between the four work-package pages with the Results scene panel controls.')
bullet(doc, 'A separate device-configuration review shows starting configuration when no terminal changes were recorded and highlights possible incorrect configuration associated with unmet WP2 and WP3 criteria.')

doc.add_paragraph('Assessment behavior and interpretation', style='Heading 1')
doc.add_paragraph('The evaluator records both completed and incomplete conditions. The customer should therefore read the Results page as an exit snapshot of the trainee’s current state, not as a fixed checklist with a permanently constant row count. Scenario-defined placements, cable requirements, permanent connections, required ports, and endpoint evidence requirements can add or remove individual criteria while preserving the same four-package structure.')
doc.add_paragraph('The assessment coordinator evaluates WP1 first, then WP2, WP3, and WP4. The overall assessment passes only when the unmet-criteria list is empty for every work package. When no session or network state is available, the affected package is intentionally marked incomplete so missing assessment data cannot be mistaken for a pass.')

doc.add_paragraph('Traceability', style='Heading 1')
add_table(doc, ['Source', 'Role in this overview'], [
    ('TssPhysicalWorkPackageEvaluator.cs', 'Defines WP1 physical placement, cable, permanent-connection, and termination checks.'),
    ('TssNetworkWorkPackageEvaluator.cs', 'Defines WP2, WP3, and WP4 live-state checks and evidence requirements.'),
    ('TssScenarioAssessment.cs', 'Combines the four work packages and defines package titles and overall pass behavior.'),
    ('TssResultsView.cs', 'Controls Results scene rendering, package paging, overall summary, and device review.'),
    ('TssResultsCard.cs', 'Renders PASS or INCOMPLETE status, verified criteria, and actionable remaining criteria.'),
    ('Results.unity', 'Provides the four work-package pages and the Results scene layout/bindings used to present the assessment.'),
], [2.25, 5.05])

doc.add_paragraph('Current interpretation for customer review', style='Heading 1')
doc.add_paragraph('The current implementation provides a complete four-package evaluation framework with live-state checks for physical installation, switching, routing, and wireless operation. The most scenario-dependent portions are WP1 placement and cabling, WP2 required access ports, and WP3 endpoint evidence. These are intentionally driven from scenario data so the same Results workflow can support more than one training scenario.')

footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
fr = footer.add_run('Customer evaluation overview  |  Unity training simulator')
fr.font.size = Pt(8); fr.font.color.rgb = RGBColor.from_string('666666')

doc.core_properties.title = 'Currently Tracked Evaluation Items'
doc.core_properties.subject = 'Work package evaluation overview and Results scene behavior'
doc.core_properties.author = 'Unity training simulator project'
# Keep Word's built-in Title style from adding its default blue rule.
for element in (styles['Title']._element, p._p):
    ppr = element.find(qn('w:pPr')) if element.tag == qn('w:p') else element.find(qn('w:pPr'))
    if ppr is not None:
        pbdr = ppr.find(qn('w:pBdr'))
        if pbdr is not None: ppr.remove(pbdr)
OUT.parent.mkdir(parents=True, exist_ok=True)
doc.save(OUT)
print(OUT)
