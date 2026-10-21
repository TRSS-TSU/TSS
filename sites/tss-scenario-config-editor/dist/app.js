const $ = id => document.getElementById(id);
const state = { manifest: null, scenarios: new Map(), selected: null };
const key = "tss-scenario-editor-draft-v1";
const required = ["inventory","cableInventory","cableTypeRequirements","permanentConnections","cableSchedule","namingRows","racks","expectedRackPlacements","expectedEndpointPlacements","networkScenario","binderReference"];
const setStatus = text => $("status").textContent = text;
const filename = id => `${id.toLowerCase().replace(/[^a-z0-9_-]+/g,"-")}.json`;
const download = (name, value) => { const a=document.createElement("a"); a.href=URL.createObjectURL(new Blob([JSON.stringify(value,null,2)+"\n"],{type:"application/json"})); a.download=name; a.click(); URL.revokeObjectURL(a.href); };
function selected() { try { return JSON.parse($("json").value); } catch { return null; } }
function errorsFor(data, manifest) {
  const errors=[]; if (!data || typeof data!=="object") return ["JSON must be an object."];
  if(data.format!=="tss-scenario"||data.schemaVersion!==1) errors.push("format must be tss-scenario and schemaVersion must be 1.");
  ["scenarioId","displayName","sceneName"].forEach(k=>!String(data[k]||"").trim()&&errors.push(`${k} is required.`));
  if(data.sceneName!=="TSS") errors.push("Only the TSS scene is supported.");
  required.forEach(k=>data[k]===undefined&&errors.push(`${k} is required.`));
  const unique=(rows,field,label)=>{const seen=new Set; (Array.isArray(rows)?rows:[]).forEach((r,i)=>{const v=String(r?.[field]??""); if(!v.trim()||v!==v.trim()||seen.has(v.toLowerCase())) errors.push(`${label} ${i+1} has a missing, padded, or duplicate ${field}.`); seen.add(v.toLowerCase());});};
  unique(data.inventory,"equipmentId","Inventory"); unique(data.cableInventory,"cableId","Cable inventory"); unique(data.permanentConnections,"connectionId","Permanent connection"); unique(data.cableTypeRequirements,"requirementId","Cable requirement");
  ["inventory","cableInventory"].forEach(k=>(data[k]||[]).forEach((r,i)=>{if(!Number.isInteger(r?.quantity)||r.quantity<0) errors.push(`${k} row ${i+1} has an invalid quantity.`)}));
  (data.racks||[]).forEach(r=>{if(!r?.rackId||r.lowestU<1||r.highestU>40||r.lowestU>r.highestU||r.firstInstallableU<r.lowestU||r.lastInstallableU>r.highestU||r.firstInstallableU>r.lastInstallableU) errors.push(`Invalid rack range: ${r?.rackId||"missing rack"}.`);});
  if(manifest){const entry=(manifest.scenarios||[]).find(x=>x.scenarioId===data.scenarioId); if(!entry) errors.push("Scenario ID is not in the manifest."); else if(entry.displayName!==data.displayName||entry.sceneName!==data.sceneName) errors.push("Scenario identity does not match its manifest entry.");}
  return errors;
}
function renderValidation(){const data=selected(), errors=errorsFor(data,state.manifest), box=$("errors"); box.innerHTML=""; if(!errors.length){box.innerHTML="<li class='ok'>Ready for Unity validation.</li>";} else errors.forEach(e=>{const li=document.createElement("li");li.textContent=e;box.append(li)}); return errors;}
function renderBinder(){const root=$("binder"), data=selected(); root.innerHTML=""; const binder=data?.binderReference; if(!binder){root.innerHTML="<p>No binder content available.</p>";return;} Object.values(binder).forEach(section=>{if(!section)return;const n=$("binderTemplate").content.cloneNode(true);n.querySelector("h3").textContent=section.sectionTitle||"Untitled section";n.querySelector("strong").textContent=section.scenarioHeading||"";n.querySelector("p").textContent=[section.intro,section.paragraph,section.note,section.endingParagraph].filter(Boolean).join("\n\n");root.append(n)});}
function save(){const data=selected(); if(!data)return; state.scenarios.set(data.scenarioId,data); localStorage.setItem(key,JSON.stringify({manifest:state.manifest,scenarios:[...state.scenarios.entries()],selected:data.scenarioId})); renderValidation(); renderBinder();}
function selectScenario(id){state.selected=id; const data=state.scenarios.get(id); $("json").value=JSON.stringify(data,null,2); $("json").disabled=false; ["validate","downloadScenario","downloadManifest"].forEach(x=>$(x).disabled=false); renderValidation();renderBinder();}
function populate(){const select=$("scenarioSelect");select.innerHTML=""; (state.manifest?.scenarios||[]).forEach(e=>{if(!state.scenarios.has(e.scenarioId))return;const o=new Option(e.displayName,e.scenarioId);select.add(o)});select.disabled=!select.options.length;if(select.options.length)selectScenario(select.value);setStatus(`${state.scenarios.size} scenario${state.scenarios.size===1?"":"s"} loaded.`)}
async function loadWorkspace(){const root="../../../Assets/StreamingAssets/Scenarios/";const manifest=await fetch(root+"manifest.json").then(r=>r.json());const scenarios=await Promise.all(manifest.scenarios.map(async e=>[e.scenarioId,await fetch(root+e.jsonFile).then(r=>r.json())]));state.manifest=manifest;state.scenarios=new Map(scenarios);populate();save();}
$("loadWorkspace").onclick=()=>loadWorkspace().catch(e=>setStatus(`Could not load shipped samples: ${e.message}`));
$("importFiles").onchange=async e=>{const files=await Promise.all([...e.target.files].map(async f=>[f.name,JSON.parse(await f.text())]));const manifest=files.map(x=>x[1]).find(x=>x?.format==="tss-scenario-manifest");if(!manifest)return setStatus("Import must include manifest.json.");state.manifest=manifest;state.scenarios=new Map(files.map(x=>x[1]).filter(x=>x?.format==="tss-scenario").map(x=>[x.scenarioId,x]));populate();save();};
$("scenarioSelect").onchange=e=>selectScenario(e.target.value); $("json").oninput=save; $("validate").onclick=()=>{const e=renderValidation();setStatus(e.length?`${e.length} issue${e.length===1?"":"s"} found.`:"Browser validation passed; run Unity tests before release.")};
$("downloadScenario").onclick=()=>{const data=selected();if(data)download(filename(data.scenarioId),data)};$("downloadManifest").onclick=()=>state.manifest&&download("manifest.json",state.manifest);$("clearDraft").onclick=()=>{localStorage.removeItem(key);setStatus("Saved draft cleared.")};
try {const draft=JSON.parse(localStorage.getItem(key));if(draft?.manifest){state.manifest=draft.manifest;state.scenarios=new Map(draft.scenarios);populate();if(draft.selected&&state.scenarios.has(draft.selected)){ $("scenarioSelect").value=draft.selected;selectScenario(draft.selected);}setStatus("Recovered local draft.");}}catch{}
