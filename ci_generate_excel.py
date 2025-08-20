# ci_generate_excel.py
import pandas as pd
import xml.etree.ElementTree as ET
from bs4 import BeautifulSoup
from collections import defaultdict
import glob, re, sys, os

XML_PREFIX = "M:BlindTreasure.UnitTest.Services."

# ---------- helpers ----------
def strip_params(s): 
    return s.split('(')[0] if s else s

def short_class(fq): 
    return fq.split('.')[-1] if fq else ''

def norm(s): 
    return re.sub(r'[^A-Za-z0-9_]', '', (s or '')).lower()

def base_function_from_test(method_name: str) -> str:
    m = strip_params(method_name or '')
    if '_Should' in m: 
        return m.split('_Should', 1)[0]
    if '_When' in m:   
        return m.split('_When', 1)[0]
    return m

def same_function(doc_func: str, test_method: str) -> bool:
    d = norm(doc_func)
    t = norm(base_function_from_test(test_method))
    if d == t: return True
    if d.endswith('async') and t == d[:-5]: return True
    if t.endswith('async') and d == t[:-5]: return True
    if len(d) > 3 and len(t) > 3 and (d in t or t in d): return True
    return False

def classify_case(name: str) -> str:
    n = (name or '').lower()
    if 'boundary' in n: return 'B'
    if 'abnormal' in n: return 'A'
    if 'normal'   in n: return 'N'
    return 'N'

def expected_from_name(name: str) -> str:
    n = (name or '').lower()
    if 'shouldreturntrue'  in n: return 'TRUE'
    if 'shouldreturnfalse' in n: return 'FALSE'
    if 'shouldthrow' in n or 'exception' in n: return 'EXCEPTION'
    return ''

def get_expected_return_value(name: str) -> str:
    n = (name or '').lower()
    if 'shouldreturntrue' in n: return 'true'
    if 'shouldreturnfalse' in n: return 'false'
    return ''

def extract_log_message_from_name(name: str) -> str:
    n = (name or '').lower()
    if 'notfound' in n: return 'Not Found'
    if 'conflict' in n: return 'Conflict'
    return ''

def safe_sheet_name(name, existing_sheets):
    if not name: name = "Sheet"
    safe_name = re.sub(r'[\\/*\[\]:?]', '_', str(name))
    max_base_length = 25
    if len(safe_name) > max_base_length: safe_name = safe_name[:max_base_length]
    final_name = safe_name
    counter = 1
    while final_name in existing_sheets:
        counter += 1
        suffix = f" ({counter})"
        if len(safe_name) + len(suffix) > 31:
            truncated = safe_name[:31 - len(suffix)]
            final_name = f"{truncated}{suffix}"
        else:
            final_name = f"{safe_name}{suffix}"
    return final_name

# ---------- XML -> functions ----------
def extract_functions():
    xml_files = glob.glob('./BlindTreasure.UnitTest/bin/**/*.xml', recursive=True)
    agg = {}
    for f in xml_files:
        try:
            root = ET.parse(f).getroot()
        except Exception:
            continue
        for m in root.findall('.//member'):
            name = m.get('name', '')
            if not name.startswith('M:'): continue
            if not name.startswith(XML_PREFIX): continue
            parts = name.split('.')
            if len(parts) < 2: continue
            cls = parts[-2]
            test_method = strip_params(parts[-1])
            func = base_function_from_test(test_method)

            desc = pre = cov = exp = ''
            s = m.find('summary')
            if s is not None and s.text: 
                desc = ' '.join(s.text.split())
            r = m.find('remarks')
            if r is not None and r.text:
                for line in r.text.splitlines():
                    line = line.strip()
                    if line.startswith('Scenario:'):
                        pre = line.replace('Scenario:', '').strip() or pre
                    elif line.startswith('Expected:'):
                        exp = line.replace('Expected:', '').strip() or exp
                    elif line.startswith('Coverage:'):
                        cov = line.replace('Coverage:', '').strip() or cov

            key = (cls, func)
            if key not in agg:
                agg[key] = {
                    "Class": cls, "Function": func,
                    "Description": desc, "PreCondition": pre,
                    "Requirement": cov, "ExpectedBehavior": exp
                }
            else:
                if not agg[key]["Description"] and desc: agg[key]["Description"] = desc
                if not agg[key]["PreCondition"] and pre: agg[key]["PreCondition"] = pre
                if not agg[key]["Requirement"] and cov:  agg[key]["Requirement"] = cov
                if not agg[key].get("ExpectedBehavior") and exp: agg[key]["ExpectedBehavior"] = exp
    return list(agg.values())

# ---------- TRX parse ----------
def parse_trx():
    candidates = glob.glob('./TestResults/**/test-results.trx', recursive=True)
    if not candidates: return []
    trx_path = sorted(candidates)[-1]
    with open(trx_path, 'r', encoding='utf-8', errors='ignore') as f:
        soup = BeautifulSoup(f.read(), 'xml')

    id_map = {}
    for ut in soup.find_all('UnitTest'):
        ut_id = ut.get('id') or ut.get('testId')
        tm = ut.find('TestMethod')
        if not ut_id or tm is None: continue
        class_full = tm.get('className', '')
        method_name = strip_params(tm.get('name', ''))
        id_map[ut_id] = (short_class(class_full), method_name)

    out = []
    for res in soup.find_all('UnitTestResult'):
        tid = res.get('testId')
        if not tid or tid not in id_map: continue
        cls, mth = id_map[tid]
        out.append({
            "Class": cls, "Method": mth,
            "Base": base_function_from_test(mth),
            "Outcome": res.get('outcome', 'Unknown'),
            "Display": strip_params(res.get('testName','')),
            "ExecutedAt": res.get('endTime') or res.get('startTime') or ''
        })
    return out

# ---------- Build core sheets ----------
def build_core():
    funcs = extract_functions()
    trx = parse_trx()
    idx = defaultdict(list)

    for t in trx:
        matched = False
        for f_ex in funcs:
            if f_ex["Class"] == t["Class"] and same_function(f_ex["Function"], t["Base"]):
                idx[(f_ex["Class"], f_ex["Function"])].append(t)
                matched = True
                break
        if not matched:
            synthetic_func = {
                "Class": t["Class"], "Function": t["Base"],
                "Description": f"Auto-generated for {t['Method']}",
                "PreCondition": "", "Requirement": "", "ExpectedBehavior": ""
            }
            exists = any(fx["Class"] == synthetic_func["Class"] and fx["Function"] == synthetic_func["Function"] for fx in funcs)
            if not exists: funcs.append(synthetic_func)
            idx[(t["Class"], t["Base"])] .append(t)

    func_rows = []
    for i, f in enumerate(funcs, 1):
        func_rows.append({
            "No": i, "RequirementName": f["Requirement"],
            "Class Name": f["Class"], "Function Name": f["Function"],
            "Function Code": f"Code_{i}",
            "Sheet Name": f["Function"], "Description": f["Description"],
            "Pre-Condition": f["PreCondition"]
        })

    stat_rows = []
    for i, f in enumerate(funcs, 1):
        tests = idx.get((f["Class"], f["Function"]), [])
        p=f_=u=0; N=A=B=0
        for t in tests:
            if t["Outcome"]=="Passed": p+=1
            elif t["Outcome"]=="Failed": f_+=1
            else: u+=1
            c = classify_case(t["Display"] or t["Method"])
            if c=='N': N+=1
            elif c=='A': A+=1
            elif c=='B': B+=1
        stat_rows.append({
            "No": i,
            "Function Code": f'=HYPERLINK("#{f["Function"]}!A1","Code_{i}")',
            "Passed": p, "Failed": f_, "Untested": u,
            "N": N,"A":A,"B":B,"Total Test Cases": p+f_+u
        })

    return funcs, pd.DataFrame(func_rows), pd.DataFrame(stat_rows), idx

# ---------- Matrix writer ----------
def write_matrix(workbook, writer, func, tests):
    sheet_name = safe_sheet_name(func["Function"], writer.sheets.keys())
    ws = workbook.add_worksheet(sheet_name)
    writer.sheets[sheet_name] = ws

    blue_header = workbook.add_format({"bold":True,"align":"center","valign":"vcenter","border":1,"bg_color":"#1e1b4b","font_color":"white"})
    blue_section= workbook.add_format({"bold":True,"align":"left","valign":"vcenter","border":1,"bg_color":"#1e1b4b","font_color":"white"})
    label_format= workbook.add_format({"align":"left","valign":"vcenter","border":1})
    data_format = workbook.add_format({"align":"center","valign":"vcenter","border":1})

    cols=[]
    for k,t in enumerate(tests,1):
        title = t["Display"] or t["Method"]
        cols.append({
            "id": f"UTCID{k:02d}","name":title,
            "exp": expected_from_name(title),
            "return_value": get_expected_return_value(title),
            "type": classify_case(title),"out":t["Outcome"],
            "dt":t["ExecutedAt"],
            "log_msg": extract_log_message_from_name(title)
        })

    ws.set_column(0,0,45)
    for j in range(len(cols)): ws.set_column(1+j,1+j,15)

    r=0
    ws.write(r,0,"",data_format)
    for j,c in enumerate(cols): ws.write(r,1+j,c["id"],blue_header)
    r+=1

    ws.write(r,0,"Condition",blue_section)
    for j in range(len(cols)): ws.write(r,1+j,"",blue_section)
    r+=1

    ws.write(r,0,"Scenario",label_format)
    for j in range(len(cols)):
        ws.write(r,1+j, func.get("PreCondition",""), data_format)
    r+=1

    ws.write(r,0,"Confirm",blue_section)
    for j in range(len(cols)): ws.write(r,1+j,"",blue_section)
    r+=1

    ws.write(r,0,"Expected Behavior",label_format)
    for j in range(len(cols)):
        val = func.get("ExpectedBehavior","") or cols[j]["exp"]
        ws.write(r,1+j,val,data_format)
    r+=1

    ws.write(r,0,"Coverage",label_format)
    for j in range(len(cols)):
        ws.write(r,1+j, func.get("Requirement",""), data_format)
    r+=1

    ws.write(r,0,"Result",blue_section)
    for j in range(len(cols)): ws.write(r,1+j,"",blue_section)
    r+=1

    ws.write(r,0,"Type(N/A/B)",label_format)
    for j,c in enumerate(cols): ws.write(r,1+j,c["type"],data_format)
    r+=1

    ws.write(r,0,"Passed/Failed",label_format)
    for j,c in enumerate(cols): 
        ws.write(r,1+j,"P" if c["out"]=="Passed" else ("F" if c["out"]=="Failed" else ""),data_format)
    r+=1

    ws.write(r,0,"Executed Date",label_format)
    for j,c in enumerate(cols):
        date_str = c["dt"].split('T')[0] if 'T' in c["dt"] else c["dt"]
        ws.write(r,1+j,date_str,data_format)
    r+=1

    ws.write(r,0,"Defect ID",label_format)
    for j in range(len(cols)): ws.write(r,1+j,"",data_format)

    return sheet_name

# ---------- main ----------
def main():
    funcs, df_funcs, df_stats, idx = build_core()
    os.makedirs("./coveragereport", exist_ok=True)
    out="./coveragereport/Functions_Statistics_Report.xlsx"

    sheet_name_mapping={}
    with pd.ExcelWriter(out, engine="xlsxwriter") as w:
        df_funcs.to_excel(w, sheet_name="Functions", index=False)
        df_stats.to_excel(w, sheet_name="Statistics", index=False)

        wb=w.book
        hdr=wb.add_format({"bold":True,"bg_color":"#1F4E78","font_color":"white","border":1})
        for sheet,df in [("Functions",df_funcs),("Statistics",df_stats)]:
            ws=w.sheets[sheet]
            for c,col in enumerate(df.columns): ws.write(0,c,col,hdr)

        for f in funcs:
            tests=idx.get((f["Class"],f["Function"]),[])
            if tests:
                actual=write_matrix(wb,w,f,tests)
                sheet_name_mapping[f["Function"]]=actual

        stats_ws=w.sheets["Statistics"]
        for i,f in enumerate(funcs):
            if f["Function"] in sheet_name_mapping:
                actual=sheet_name_mapping[f["Function"]]
                stats_ws.write(i+1,1,f'=HYPERLINK("#{actual}!A1","Code_{i+1}")')

    print("Report generated:", out)

if __name__=="__main__":
    sys.exit(main())
