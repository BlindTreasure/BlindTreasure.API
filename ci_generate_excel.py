# ci_generate_excel.py
import pandas as pd
import xml.etree.ElementTree as ET
from bs4 import BeautifulSoup
from collections import defaultdict, OrderedDict
import glob, re, sys, os
from datetime import datetime

# Tùy biến theo project test: namespace của test methods trong XML docs
XML_PREFIX = "M:BlindTreasure.UnitTest.Services."

# ----------------- Helpers -----------------
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

# ---------- XML: đọc per-test (không gộp) ----------
def parse_xml_docs_per_test():
    """
    Trả về dict[(Class, Method)] = {
        'Summary', 'Scenario', 'Expected', 'Coverage'
    }
    """
    docs = {}
    xml_files = glob.glob('./BlindTreasure.UnitTest/bin/**/*.xml', recursive=True)
    for f in xml_files:
        try:
            root = ET.parse(f).getroot()
        except Exception:
            continue
        for m in root.findall('.//member'):
            name = m.get('name', '')
            if not name.startswith('M:'): 
                continue
            if not name.startswith(XML_PREFIX): 
                continue
            parts = name.split('.')
            if len(parts) < 2:
                continue
            cls = parts[-2]
            method = strip_params(parts[-1])

            summary = scenario = expected = coverage = ''
            s = m.find('summary')
            if s is not None and s.text:
                summary = ' '.join(s.text.split())
            r = m.find('remarks')
            if r is not None and r.text:
                for line in r.text.splitlines():
                    line = line.strip()
                    if line.startswith('Scenario:'):
                        scenario = line.replace('Scenario:', '').strip() or scenario
                    elif line.startswith('Expected:'):
                        expected = line.replace('Expected:', '').strip() or expected
                    elif line.startswith('Coverage:'):
                        coverage = line.replace('Coverage:', '').strip() or coverage

            docs[(cls, method)] = {
                "Summary": summary,
                "Scenario": scenario,
                "Expected": expected,
                "Coverage": coverage
            }
    return docs

# ---------- TRX parse ----------
def parse_trx():
    candidates = glob.glob('./TestResults/**/test-results.trx', recursive=True)
    if not candidates: 
        return []
    trx_path = sorted(candidates)[-1]
    with open(trx_path, 'r', encoding='utf-8', errors='ignore') as f:
        soup = BeautifulSoup(f.read(), 'xml')

    id_map = {}
    for ut in soup.find_all('UnitTest'):
        ut_id = ut.get('id') or ut.get('testId')
        tm = ut.find('TestMethod')
        if not ut_id or tm is None:
            continue
        class_full = tm.get('className', '')
        method_name = strip_params(tm.get('name', ''))
        id_map[ut_id] = (short_class(class_full), method_name)

    out = []
    for res in soup.find_all('UnitTestResult'):
        tid = res.get('testId')
        if not tid or tid not in id_map:
            continue
        cls, mth = id_map[tid]
        out.append({
            "Class": cls,
            "Method": mth,                               # test method
            "Base": base_function_from_test(mth),        # function-under-test
            "Outcome": res.get('outcome', 'Unknown'),    # Passed/Failed/NotExecuted
            "Display": strip_params(res.get('testName','')),
            "ExecutedAt": res.get('endTime') or res.get('startTime') or ''
        })
    return out

# ---------- Heuristics từ Expected / tên test ----------
ERROR_KEYWORDS = [
    'not found', 'conflict', 'bad request', 'unauthorized', 'forbidden',
    'invalid', 'error', 'exception', 'missing', 'null', 'empty', 'failed'
]

def parse_expected_kind(expected_text: str, test_name: str):
    """
    Trả về one of: 'TRUE' | 'FALSE' | 'EXCEPTION' | ''
    Ưu tiên Expected trong XML; fallback theo tên test.
    """
    e = (expected_text or '').lower()
    n = (test_name or '').lower()

    # XML first
    if 'true' in e and 'not' not in e:
        return 'TRUE'
    if 'false' in e:
        return 'FALSE'
    if any(k in e for k in ERROR_KEYWORDS):
        return 'EXCEPTION'

    # Fallback theo tên test
    if 'shouldreturntrue' in n or 'should_be_true' in n:
        return 'TRUE'
    if 'shouldreturnfalse' in n or 'should_be_false' in n:
        return 'FALSE'
    if 'shouldthrow' in n or 'exception' in n or any(k in n for k in ERROR_KEYWORDS):
        return 'EXCEPTION'
    return ''

def derive_log_message(expected_text: str, test_name: str):
    """
    Trích message để hiển thị trong phần Log message.
    - Nếu Expected có chuỗi trong dấu nháy đơn -> lấy chuỗi đó (giữ nguyên).
    - Nếu Expected chứa từ khóa lỗi -> dựng message chuẩn hóa (Title Case).
    - Nếu TRUE/FALSE đơn thuần -> trả rỗng.
    """
    txt = (expected_text or '').strip()
    if not txt:
        return ''

    # ưu tiên chuỗi trong dấu nháy đơn
    quoted = re.findall(r"'([^']+)'", txt)
    if quoted:
        return f"\"{quoted[0]}\""

    low = txt.lower()
    if 'not found' in low: return "\"Not Found\""
    if 'conflict' in low: return "\"Conflict\""
    if 'bad request' in low: return "\"Bad Request\""
    if 'unauthorized' in low: return "\"Unauthorized\""
    if 'forbidden' in low: return "\"Forbidden\""
    if 'invalid' in low: return "\"Invalid input\""
    if 'missing' in low: return "\"Missing input\""
    if 'error' in low or 'exception' in low: return "\"Error\""

    # TRUE/FALSE kiểu success -> có thể để thông điệp thành “Success”
    if 'true' in low and 'not' not in low:
        return "\"Success\""

    return ''

def classify_type(kind: str, test_name: str):
    """
    kind = TRUE/FALSE/EXCEPTION
    - EXCEPTION hoặc FALSE => A
    - có 'boundary' trong tên => B
    - còn lại => N
    """
    n = (test_name or '').lower()
    if 'boundary' in n:
        return 'B'
    if kind in ('EXCEPTION', 'FALSE'):
        return 'A'
    return 'N'

def format_date_dmy(iso_dt: str):
    if not iso_dt:
        return ''
    try:
        # 2025-08-19T10:05:12.3456789Z -> 19/08/2025
        dt = iso_dt.split('T')[0]
        y, m, d = dt.split('-')
        return f"{d}/{m}/{y}"
    except Exception:
        return iso_dt

# ---------- Build sheets ----------
def build_core():
    # docs per-test
    docs_map = parse_xml_docs_per_test()
    # trx
    raw_tests = parse_trx()

    # enrich tests with XML docs
    tests = []
    for t in raw_tests:
        doc = docs_map.get((t["Class"], t["Method"]), {})
        t2 = dict(t)
        t2["Summary"] = doc.get("Summary", "")
        t2["Scenario"] = doc.get("Scenario", "")
        t2["ExpectedText"] = doc.get("Expected", "")
        t2["Coverage"] = doc.get("Coverage", "")
        tests.append(t2)

    # group by function under test
    groups = defaultdict(list)
    for t in tests:
        groups[(t["Class"], t["Base"])].append(t)

    # Functions sheet rows
    func_rows = []
    funcs_info = []
    counter = 1
    for (cls, func), lst in groups.items():
        # lấy summary/coverage đầu tiên có dữ liệu
        desc = next((x["Summary"] for x in lst if x.get("Summary")), "")
        precond = next((x["Scenario"] for x in lst if x.get("Scenario")), "")
        coverage = ", ".join(sorted({x["Coverage"] for x in lst if x.get("Coverage")}))

        funcs_info.append({"Class": cls, "Function": func, "Description": desc,
                           "PreCondition": precond, "Requirement": coverage})

        func_rows.append({
            "No": counter,
            "RequirementName": coverage,
            "Class Name": cls,
            "Function Name": func,
            "Function Code": f"Code_{counter}",
            "Sheet Name": func,
            "Description": desc,
            "Pre-Condition": precond
        })
        counter += 1

    # Statistics sheet rows
    stat_rows = []
    counter = 1
    for info in funcs_info:
        lst = groups[(info["Class"], info["Function"])]
        p = f_ = u = 0
        N = A = B = 0
        for t in lst:
            if t["Outcome"] == "Passed": p += 1
            elif t["Outcome"] == "Failed": f_ += 1
            else: u += 1

            kind = parse_expected_kind(t["ExpectedText"], t["Display"] or t["Method"])
            tp = classify_type(kind, t["Display"] or t["Method"])
            if tp == 'N': N += 1
            elif tp == 'A': A += 1
            elif tp == 'B': B += 1

        stat_rows.append({
            "No": counter,
            "Function Code": f'=HYPERLINK("#{info["Function"]}!A1","Code_{counter}")',
            "Passed": p, "Failed": f_, "Untested": u,
            "N": N, "A": A, "B": B,
            "Total Test Cases": p + f_ + u
        })
        counter += 1

    return funcs_info, pd.DataFrame(func_rows), pd.DataFrame(stat_rows), groups

# ---------- Matrix writer (đúng layout mẫu) ----------
def write_matrix(workbook, writer, info, tests):
    """
    info: {Class, Function, Description, PreCondition, Requirement}
    tests: list các test (đã enrich Scenario/Expected/Outcome/ExecutedAt)
    """
    # chuẩn hóa dữ liệu per-column
    cols = []
    for k, t in enumerate(tests, 1):
        title = t["Display"] or t["Method"]
        scenario = (t.get("Scenario") or "").strip()
        if not scenario:
            scenario = "Standard test condition"

        kind = parse_expected_kind(t.get("ExpectedText",""), title)
        # TRUE/FALSE mark
        is_true = (kind == 'TRUE')
        is_false = (kind == 'FALSE')
        # log message
        log_msg = derive_log_message(t.get("ExpectedText",""), title)

        cols.append({
            "id": f"UTCID{k:02d}",
            "name": title,
            "scenario": scenario,
            "kind": kind,                 # TRUE / FALSE / EXCEPTION / ''
            "log_msg": log_msg,           # string or ''
            "type": classify_type(kind, title),
            "outcome": t["Outcome"],      # Passed/Failed/NotExecuted
            "date": format_date_dmy(t["ExecutedAt"])
        })

    # tập hợp tất cả scenario thành các dòng Input (duy nhất, theo thứ tự xuất hiện)
    all_inputs = []
    seen = set()
    for c in cols:
        s = c["scenario"]
        if s and s not in seen:
            all_inputs.append(s)
            seen.add(s)

    # tập hợp tất cả log messages (duy nhất, theo thứ tự)
    all_msgs = []
    seenm = set()
    for c in cols:
        m = c["log_msg"]
        if m and m not in seenm:
            all_msgs.append(m)
            seenm.add(m)

    # sheet & formats
    sheet_name = safe_sheet_name(info["Function"], writer.sheets.keys())
    ws = workbook.add_worksheet(sheet_name)
    writer.sheets[sheet_name] = ws

    blue_header = workbook.add_format({"bold": True, "align": "center", "valign": "vcenter",
                                       "border": 1, "bg_color": "#1e1b4b", "font_color": "white"})
    blue_section = workbook.add_format({"bold": True, "align": "left", "valign": "vcenter",
                                        "border": 1, "bg_color": "#1e1b4b", "font_color": "white"})
    label_format = workbook.add_format({"align": "left", "valign": "vcenter", "border": 1})
    data_format  = workbook.add_format({"align": "center", "valign": "vcenter", "border": 1})

    # width
    ws.set_column(0, 0, 45)
    for j in range(len(cols)): ws.set_column(1 + j, 1 + j, 15)

    r = 0
    # Header UTCID
    ws.write(r, 0, "", data_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, c["id"], blue_header)
    r += 1

    # -------------- Condition --------------
    ws.write(r, 0, "Condition", blue_section)
    for j in range(len(cols)): ws.write(r, 1 + j, "", blue_section)
    r += 1

    ws.write(r, 0, "Precondition", label_format)
    for j in range(len(cols)): ws.write(r, 1 + j, "", data_format)
    r += 1

    # Input rows (one per scenario)
    for inp in all_inputs:
        ws.write(r, 0, f"Input: {inp}", label_format)
        for j, c in enumerate(cols):
            ws.write(r, 1 + j, "O" if c["scenario"] == inp else "", data_format)
        r += 1

    # -------------- Confirm --------------
    ws.write(r, 0, "Confirm", blue_section)
    for j in range(len(cols)): ws.write(r, 1 + j, "", blue_section)
    r += 1

    # Return
    ws.write(r, 0, "Return", label_format)
    for j in range(len(cols)): ws.write(r, 1 + j, "", data_format)
    r += 1

    # FALSE row
    ws.write(r, 0, "FALSE", label_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, "O" if c["kind"] == "FALSE" else "", data_format)
    r += 1

    # TRUE row
    ws.write(r, 0, "TRUE", label_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, "O" if c["kind"] == "TRUE" else "", data_format)
    r += 1

    # Exception header
    ws.write(r, 0, "Exception", label_format)
    for j in range(len(cols)): ws.write(r, 1 + j, "", data_format)
    r += 1

    # Log message section
    ws.write(r, 0, "Log message", label_format)
    for j in range(len(cols)): ws.write(r, 1 + j, "", data_format)
    r += 1

    for msg in all_msgs:
        ws.write(r, 0, msg, label_format)
        for j, c in enumerate(cols):
            ws.write(r, 1 + j, "O" if c["log_msg"] == msg else "", data_format)
        r += 1

    # -------------- Result --------------
    ws.write(r, 0, "Result", blue_section)
    for j in range(len(cols)): ws.write(r, 1 + j, "", blue_section)
    r += 1

    ws.write(r, 0, "Type(N : Normal, A : Abnormal, B : Boundary)", label_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, c["type"], data_format)
    r += 1

    ws.write(r, 0, "Passed/Failed", label_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, "P" if c["outcome"] == "Passed" else ("F" if c["outcome"] == "Failed" else ""), data_format)
    r += 1

    ws.write(r, 0, "Executed Date", label_format)
    for j, c in enumerate(cols):
        ws.write(r, 1 + j, c["date"], data_format)
    r += 1

    # cuối cùng 1 hàng trống “Defect ID”
    ws.write(r, 0, "Defect ID", label_format)
    for j in range(len(cols)): ws.write(r, 1 + j, "", data_format)

    return sheet_name

# ---------- main ----------
def main():
    funcs_info, df_funcs, df_stats, groups = build_core()
    os.makedirs("./coveragereport", exist_ok=True)
    out = "./coveragereport/Functions_Statistics_Report.xlsx"

    sheet_name_mapping = {}
    with pd.ExcelWriter(out, engine="xlsxwriter") as w:
        # core sheets
        df_funcs.to_excel(w, sheet_name="Functions", index=False)
        df_stats.to_excel(w, sheet_name="Statistics", index=False)

        wb = w.book
        hdr = wb.add_format({"bold": True, "bg_color": "#1F4E78", "font_color": "white", "border": 1})
        for sheet, df in [("Functions", df_funcs), ("Statistics", df_stats)]:
            ws = w.sheets[sheet]
            for c, col in enumerate(df.columns):
                ws.write(0, c, col, hdr)

        # matrix per function
        for info in funcs_info:
            tests = groups[(info["Class"], info["Function"])]
            if tests:
                actual = write_matrix(wb, w, info, tests)
                sheet_name_mapping[info["Function"]] = actual

        # fix hyperlink to actual sheet names
        stats_ws = w.sheets["Statistics"]
        for i, info in enumerate(funcs_info):
            if info["Function"] in sheet_name_mapping:
                actual = sheet_name_mapping[info["Function"]]
                stats_ws.write(i + 1, 1, f'=HYPERLINK("#{actual}!A1","Code_{i+1}")')

    # log summary
    total_tests = int(df_stats["Total Test Cases"].sum()) if not df_stats.empty else 0
    print("Report generated:", out)
    print("Total functions:", len(funcs_info))
    print("Total test cases in report:", total_tests)

if __name__ == "__main__":
    sys.exit(main())
