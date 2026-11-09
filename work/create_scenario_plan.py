from pathlib import Path
from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor

OUT = Path(r"C:\Users\Admin\Documents\Codex\2026-11-05\referenced-chatgpt-conversation-this-is-an-2\outputs")
OUT.mkdir(parents=True, exist_ok=True)
DOCX = OUT / "TSS2_Scenario_Authoring_Custom_Scene_Implementation_Plan.docx"

doc = Document()
section = doc.sections[0]
section.top_margin = Inches(0.7)
section.bottom_margin = Inches(0.65)
section.left_margin = Inches(0.8)
section.right_margin = Inches(0.8)

styles = doc.styles
styles["Normal"].font.name = "Aptos"
styles["Normal"].font.size = Pt(10)
styles["Normal"].paragraph_format.space_after = Pt(5)
styles["Normal"].paragraph_format.line_spacing = 1.08
for name, size in (("Title", 22), ("Heading 1", 15), ("Heading 2", 12)):
    style = styles[name]
    style.font.name = "Aptos Display" if name == "Title" else "Aptos"
    style.font.size = Pt(size)
    style.font.bold = True
    style.font.color.rgb = RGBColor(0, 0, 0)

def remove_style_paragraph_borders(style):
    p_pr = style._element.get_or_add_pPr()
    for child in list(p_pr):
        if child.tag == qn("w:pBdr"):
            p_pr.remove(child)

remove_style_paragraph_borders(styles["Title"])

def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)

def borders(table, color="D9D9D9"):
    tbl = table._tbl
    tbl_pr = tbl.tblPr
    borders_el = tbl_pr.first_child_found_in("w:tblBorders")
    if borders_el is None:
        borders_el = OxmlElement("w:tblBorders")
        tbl_pr.append(borders_el)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        tag = "w:" + edge
        element = borders_el.find(qn(tag))
        if element is None:
            element = OxmlElement(tag)
            borders_el.append(element)
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), "4")
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)

def remove_paragraph_borders(paragraph):
    p_pr = paragraph._p.get_or_add_pPr()
    for child in list(p_pr):
        if child.tag == qn("w:pBdr"):
            p_pr.remove(child)

def table(rows, widths=None):
    t = doc.add_table(rows=0, cols=len(rows[0]))
    t.autofit = False
    for i, values in enumerate(rows):
        cells = t.add_row().cells
        for j, value in enumerate(values):
            cells[j].text = value
            cells[j].vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if widths:
                cells[j].width = Inches(widths[j])
            for p in cells[j].paragraphs:
                p.paragraph_format.space_after = Pt(2)
                if i == 0:
                    for run in p.runs:
                        run.bold = True
        if i == 0:
            for cell in cells:
                shade(cell, "1F4E78")
                for p in cell.paragraphs:
                    for run in p.runs:
                        run.font.color.rgb = RGBColor(255, 255, 255)
        elif i % 2 == 0:
            for cell in cells:
                shade(cell, "F3F6F8")
    borders(t)
    doc.add_paragraph().paragraph_format.space_after = Pt(1)
    return t

def bullet(text, level=0):
    p = doc.add_paragraph(style="List Bullet" if level == 0 else "List Bullet 2")
    p.add_run(text)
    return p

title = doc.add_paragraph("TSS2 Scenario Authoring and Custom Scene Implementation Plan", style="Title")
remove_paragraph_borders(title)
p = doc.add_paragraph()
p.add_run("Purpose. ").bold = True
p.add_run("Prepare the TSS2 Unity project so instructors can author new scenario requirements through the local scenario configuration editor and launch each scenario in its own Unity scene.")
p = doc.add_paragraph()
p.add_run("Outcome. ").bold = True
p.add_run("Scenario JSON becomes the authoring source for runtime evaluation requirements, while scene, catalog, and manifest validation form a strict, reusable path for future custom scenarios.")

doc.add_heading("Implementation scope", level=1)
doc.add_paragraph("The work builds on the existing scenario-driven evaluator and preserves the current Capstone/TSS behavior. It is additive: existing TSS scenarios remain valid, deterministic VLAN and IP generation remains seed-driven, and the runtime UI continues to display scenario-authored objective headings grouped over evaluator conditions.")

table([
    ["Area", "Target behavior"],
    ["Scenario data", "Author device roles, hostnames, ports, trunks, unused ranges, router interfaces, hardening flags, evidence, and objective groups."],
    ["Runtime profile", "Copy authored requirements into the scenario-derived profile; continue generating VLAN IDs and IP networks from profileSeed and student ID rules."],
    ["Evaluator", "Read requirements from the runtime profile instead of embedding scenario-specific ports, role names, interface names, or evidence IDs."],
    ["Editor", "Expose and preserve nested requirement arrays and allow valid custom scene names."],
    ["Scene launch", "Load the scene named by the selected scenario after validating Build Settings and catalog compatibility."],
])

doc.add_heading("Phase 1 Scenario requirement authoring", level=1)
doc.add_paragraph("Complete the scenario data model and runtime profile so new scenarios can describe their evaluation contract without changing evaluator code.")
for text in [
    "Keep authored values such as credentials, MOTD, VLAN names, evidence IDs, role bindings, interface names, port ranges, and objective headings in NetworkScenarioConfig.",
    "Use roleBindings to identify scenario roles and expected hostnames, including the router role and both switch roles.",
    "Use requiredPorts for access-port requirements, trunkRequirements for trunk interfaces and allowed VLAN names, and unusedPortRequirements for dead-VLAN shutdown ranges.",
    "Use routerSubinterfaceRequirements for VLAN/network mapping, address suffixes, and masks. The network base addresses and VLAN numbers remain generated by the runtime seed.",
    "Use explicit flags for optional grading requirements such as hardening controls, route evidence, default route, save state, AP checks, laptop checks, diagnostics, and RedWolf evidence.",
    "Retain defaults that reproduce the current Capstone behavior when older scenarios omit new fields."
]: bullet(text)

doc.add_heading("Phase 2 Evaluator migration", level=1)
doc.add_paragraph("Replace remaining scenario-specific constants in the evaluation path with values from TssNetworkRunProfile. Preserve condition IDs used by the HUD and results scene unless a deliberate schema migration is required.")
table([
    ["Evaluator requirement", "Scenario source"],
    ["Switch roles and expected hostnames", "roleBindings"],
    ["Access ports and endpoint VLAN mapping", "requiredPorts plus seed-derived VLAN profile"],
    ["Trunk interfaces, native VLAN, allowed VLANs", "trunkRequirements"],
    ["Unused port ranges and dead VLAN", "unusedPortRequirements"],
    ["Router role and parent/WAN interfaces", "routerRole, routerParentInterface, routerWanInterface"],
    ["Router subinterfaces", "routerSubinterfaceRequirements plus seed-derived networks"],
    ["Hardening expectations", "credentials, MOTD, role hostname, and hardening flags"],
    ["Route and endpoint evidence", "routeEvidenceCommand and evidenceRequirements"],
    ["WP4 checks", "AP/laptop role, switchport, evidence IDs, and requirement flags"],
])
doc.add_paragraph("Keep technical comparison logic in the evaluator. JSON should state what the scenario requires; the evaluator should determine whether the live state satisfies it.")

doc.add_heading("Phase 3 Custom scene and catalog support", level=1)
doc.add_paragraph("Generalize the current TSS-only restrictions so every valid scenario can reference a unique Unity scene and a compatible catalog.")
for text in [
    "Allow any non-empty, valid sceneName in the manifest and scenario JSON.",
    "Require Application.CanStreamedLevelBeLoaded(sceneName) before accepting a scenario.",
    "Remove manifest filtering that currently requires entry.sceneName == TSS.",
    "Update catalog validation so the catalog sceneName must match the scenario sceneName instead of requiring the literal TSS value.",
    "Keep TSS_MAIN_CATALOG and the current TSS scene as the existing baseline.",
    "Ensure each custom scene is added to Unity Build Settings and contains the bindings required by its scenario catalog."
]: bullet(text)

doc.add_heading("Phase 4 Scenario editor support", level=1)
doc.add_paragraph("Update the local tss-scenario-config-editor site without introducing a new editor framework.")
for text in [
    "Remove the validation message that only permits the TSS scene.",
    "Validate that sceneName is present and matches the project’s supported scene-name format.",
    "Keep manifest identity fields synchronized with the scenario JSON.",
    "Continue recursive editing of networkScenario arrays and preserve unknown fields through Advanced JSON.",
    "Add concise help text explaining that the scene must exist in Build Settings and match the selected catalog.",
    "Provide default row templates for every new requirement array."
]: bullet(text)

doc.add_heading("Files and classes to inspect", level=1)
table([
    ["File or area", "Responsibility"],
    ["Assets/Scripts/TssScenarioDefinition.cs", "Scenario and network requirement schema."],
    ["Assets/Scripts/TssNetworkSessionState.cs", "Runtime profile creation and derived seed-based values."],
    ["Assets/Scripts/TssNetworkWorkPackageEvaluator.cs", "WP2-WP4 evaluation against authored requirements."],
    ["Assets/Scripts/TssExternalScenarioLoader.cs", "Manifest loading, catalog resolution, scene availability, and scenario creation."],
    ["Assets/Scripts/TssExternalScenarioValidator.cs", "Scenario and requirement validation."],
    ["Assets/Scripts/TssScenarioCatalog.cs", "Catalog identity and scene compatibility validation."],
    ["Assets/Scripts/TssScenarioStartupMenu.cs", "Selected scenario launch and scene transition."],
    ["sites/tss-scenario-config-editor/dist/app.js", "Guided editor, validation, persistence, and download behavior."],
    ["Assets/StreamingAssets/Scenarios/manifest.json", "Scenario-to-file, catalog, and scene registration."],
])

doc.add_heading("Acceptance criteria", level=1)
for text in [
    "Unity compiles with zero errors and zero warnings.",
    "The existing Capstone scenario loads and launches in the current TSS scene.",
    "A test scenario with a unique scene name can pass manifest, scenario, catalog, and Build Settings validation.",
    "A scenario referencing a missing scene is rejected before launch.",
    "A scenario whose scene and catalog do not match is rejected.",
    "The editor accepts, edits, preserves, and downloads a custom sceneName.",
    "The editor exposes the new evaluation requirement arrays and scalar flags.",
    "WP1-WP4 evaluation results continue to use scenario-derived runtime values.",
    "VLAN and IP generation remains deterministic and student-ID seed variation remains unchanged.",
    "Existing TSS fallback behavior and scene/prefab bindings remain intact."
]: bullet(text)

doc.add_heading("Validation plan", level=1)
table([
    ["Check", "Evidence"],
    ["Static JSON", "ConvertFrom-Json succeeds for each scenario and manifest."],
    ["Editor validation", "Guided editor reports the scenario ready for Unity validation."],
    ["Unity compilation", "Unity console has zero errors and warnings."],
    ["Runtime profile", "Logged profile confirms authored requirements and unchanged seed-derived VLAN/IP values."],
    ["Evaluator regression", "Focused checks cover default Capstone requirements and a custom requirement variation."],
    ["Scene launch", "Current TSS and a unique test scene both launch through the scenario menu."],
])

doc.add_heading("Constraints and rollback", level=1)
doc.add_paragraph("Do not remove the individual-tile map path, rewrite user-authored prefab layouts, or move scripts. Preserve serialized field names. If custom scene support is not ready for all catalogs, keep the existing TSS restriction temporarily and fail clearly rather than silently launching the wrong scene.")
doc.add_paragraph("Rollback is limited to reverting the new scenario schema fields, profile copies, evaluator selectors, editor validation, and scene/catalog validation changes. Existing Capstone fields and the TSS baseline should remain usable throughout the work.")

doc.add_paragraph("Implementation status: planning document. No custom scene or loader changes are authorized by this document alone.")

footer = section.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.add_run("TSS2 Scenario Authoring Implementation Plan")
for run in footer.runs:
    run.font.size = Pt(8)
    run.font.color.rgb = RGBColor(100, 100, 100)

doc.core_properties.title = "TSS2 Scenario Authoring and Custom Scene Implementation Plan"
doc.core_properties.subject = "Implementation plan for scenario-driven evaluation and unique Unity scenes"
doc.core_properties.author = "Codex"
doc.save(DOCX)
print(DOCX)
