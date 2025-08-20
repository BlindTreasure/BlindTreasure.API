import os, re, glob, json, xml.etree.ElementTree as ET
from collections import defaultdict, OrderedDict
from datetime import datetime
import pandas as pd

CS_GLOBS = ["**/*Tests.cs", "**/*Test.cs"]
TRX_GLOB = "./TestResults/**/test-results.trx"

RE_FACT = re.compile(r'^\s*\[Fact(?:\s*\(.*?\))?\]\s*$', re.M)
RE_METHOD_SIG = re.compile(r'^\s*(?:public|internal|protected|private)\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(', re.M)
RE_CALLEE = re.compile(r'_authService\.(\w+)\s*\(')  # map test -> function under test (AuthService)
RE_DOC = re.compile(r'^\s*///\s?(.*)$', re.M)

def sanitize_sheet(name: str) -> str:
    invalid = '[]:*?/\\'
    for ch in invalid:
        name = name.replace(ch, ' ')
    return name[:31]

def parse_cs_files():
    """Extract tests with their XML doc blocks and infer function-under-test."""
    items = []  # each: dict(Class, TestMethod, Function, TestType, Description, PreCondition, Coverage, ExpectedResult, ExceptionExpected, LogMessage)
    for pattern in CS_GLOBS:
        for path in glob.glob(pattern, recursive=True):
            with open(path, 'r', encoding='utf-8', errors='ignore') as f:
                src = f.read()

            # best-effort split tests
            # find positions of [Fact] and capture the following method
            for fact_match in RE_FACT.finditer(src):
                start = fact_match.end()
                # capture doc comments immediately preceding [Fact]
                # backtrack to previous non-empty line(s) that start with ///
                doc_lines = []
                i = fact_match.start()
                while i > 0:
                    j = src.rfind('\n', 0, i-1)
                    line = src[j+1:i].rstrip('\r\n')
                    if RE_DOC.match(line):
                        doc_lines.append(RE_DOC.match(line).group(1))
                        i = j
                        continue
                    # stop at first non-doc line
                    break
                doc_lines = list(reversed(doc_lines))
                doc = "\n".join(doc_lines)

                # grab method signature and body region
                m = RE_METHOD_SIG.search(src, start)
                if not m:
                    continue
                test_method = m.group(1)

                # slice a reasonable window after method for callee detection
                window = src[m.end(): m.end()+800]
                callee = None
                mc = RE_CALLEE.search(window)
                if mc:
                    callee = mc.group(1)

                # parse fields from XML remarks (loose keys in English/VN)
                def pick(key):
                    # accept: "Scenario:", "Coverage:", "TestType:", "InputConditions:", "ExpectedResult:", "ExpectedReturnValue:", "ExceptionExpected:", "LogMessage:"
                    rx = re.compile(rf'^\s*{key}\s*:\s*(.+)$', re.I | re.M)
                    mm = rx.search(doc)
                    return mm.group(1).strip() if mm else ""

                description = pick("Scenario") or pick("Description")
                pre = pick("InputConditions") or pick("Pre-Condition") or pick("Precondition")
                coverage = pick("Coverage") or pick("Requirement")
                testtype = pick("TestType")
                expected_result = pick("ExpectedResult") or pick("ExpectedReturnValue")
                exception_expected = pick("ExceptionExpected").lower() in ("true","1","yes")
                log_message = pick("LogMessage")

                items.append({
                    "Class": os.path.splitext(os.path.basename(path))[0],
                    "TestMethod": test_method,
                    "Function": callee or "Unknown",
                    "TestType": testtype or "",
                    "Description": description,
                    "PreCondition": pre,
                    "Coverage": coverage,
                    "Expected": expected_result,
                    "Exception": exception_expected,
                    "LogMessage": log_message,
                    "File": path
                })
    return items

def parse_trx_results():
    trx_files = glob.glob(TRX_GLOB, recursive=True)
    if not trx_files:
        return {}
    trx_path = sorted(trx_files)[-1]
    tree = ET.parse(trx_path)
    root = tree.getroot()
    ns = {'t': root.tag.split('}')[0].strip('{')}

    results = {}
    for ut in root.findall('.//t:UnitTestResult', ns):
        test_name = ut.attrib.get('testName', '')
        outcome = ut.attrib.get('outcome', 'Unknown')
        results[test_name] = outcome  # Passed/Failed/NotExecuted
    return results

def build_functions_sheet(extracted):
    # unique by (Class, Function)
    agg = OrderedDict()
    for x in extracted:
        key = (x["Class"], x["Function"])
        if key not in agg:
            sheet_name = sanitize_sheet(f'{x["Function"]}')
            agg[key] = {
                "No": len(agg)+1,
                "RequirementName": x["Coverage"],
                "Class Name": x["Class"],
                "Function Name": x["Function"],
                "Sheet Name": sheet_name,
                "Description": x["Description"],
                "Pre-Condition": x["PreCondition"],
            }
        else:
            # fill blanks
            if not agg[key]["Description"] and x["Description"]:
                agg[key]["Description"] = x["Description"]
            if not agg[key]["Pre-Condition"] and x["PreCondition"]:
                agg[key]["Pre-Condition"] = x["PreCondition"]
            if not agg[key]["RequirementName"] and x["Coverage"]:
                agg[key]["RequirementName"] = x["Coverage"]
    return list(agg.values()), list(agg.keys())

def build_matrices_per_function(extracted, keys_order, trx_map):
    # build a matrix sheet per (Class, Function)
    sheets = {}  # sheet_name -> DataFrame
    group = defaultdict(list)
    for x in extracted:
        group[(x["Class"], x["Function"])].append(x)

    for key in keys_order:
        tests = group.get(key, [])
        # columns: UTCIDxx by order of appearance
        cols = [f'UTCID{str(i+1).zfill(2)}' for i in range(len(tests))]
        # build common rows
        rows = OrderedDict()
        rows["Condition"] = [""] * len(cols)
        rows["Precondition"] = [""] * len(cols)
        rows["Confirm/Return"] = [""] * len(cols)
        rows["Exception"] = [""] * len(cols)
        rows["Log message"] = [""] * len(cols)
        rows["Result"] = [""] * len(cols)  # P/F/U
        rows["Type(N/A/B)"] = [""] * len(cols)
        rows["Executed Date"] = [""] * len(cols)

        for i, t in enumerate(tests):
            rows["Precondition"][i] = t["PreCondition"]
            rows["Condition"][i] = t["Description"]
            rows["Confirm/Return"][i] = t["Expected"]
            rows["Exception"][i] = "Y" if t["Exception"] else ""
            rows["Log message"][i] = t["LogMessage"]
            outcome = trx_map.get(t["TestMethod"], "")
            if outcome == "Passed":
                rows["Result"][i] = "P"
            elif outcome == "Failed":
                rows["Result"][i] = "F"
            else:
                rows["Result"][i] = "U"
            tt = (t["TestType"] or "").strip().lower()
            rows["Type(N/A/B)"][i] = "N" if "normal" in tt else ("A" if "abnormal" in tt else ("B" if "boundary" in tt else ""))
            rows["Executed Date"][i] = datetime.utcnow().strftime("%Y-%m-%d")

        df = pd.DataFrame(rows, index=cols).T
        sheet_name = sanitize_sheet(key[1])  # Function name
        sheets[sheet_name] = df

    return sheets

def build_statistics_sheet(matrices):
    stats = []
    for i, (sheet, df) in enumerate(matrices.items(), start=1):
        # count by row "Result" across columns -> pivot: we stored results in a row; rotate back:
        result_row = df.loc["Result"]
        passed = (result_row == "P").sum()
        failed = (result_row == "F").sum()
        untested = (result_row == "U").sum()

        type_row = df.loc["Type(N/A/B)"]
        n = (type_row == "N").sum()
        a = (type_row == "A").sum()
        b = (type_row == "B").sum()

        total = len(result_row)
        stats.append({
            "No": i,
            "Function code": f'=HYPERLINK("#\'{sheet}\'!A1","Code")',
            "Passed": passed,
            "Failed": failed,
            "Untested": untested,
            "N": n, "A": a, "B": b,
            "Total Test Cases": total
        })
    return pd.DataFrame(stats)

def write_excel(functions_df, statistics_df, matrices):
    out = "./coveragereport/Functions_Statistics_Report.xlsx"
    with pd.ExcelWriter(out, engine="xlsxwriter") as xw:
        functions_df.to_excel(xw, sheet_name="Functions", index=False)
        statistics_df.to_excel(xw, sheet_name="Statistics", index=False)
        for name, df in matrices.items():
            df.to_excel(xw, sheet_name=name, index=True)
    print(f"Wrote {out}")

def main():
    extracted = parse_cs_files()
    if not extracted:
        raise SystemExit("No tests extracted")

    trx_map = parse_trx_results()
    functions_rows, keys_order = build_functions_sheet(extracted)
    functions_df = pd.DataFrame(functions_rows)

    matrices = build_matrices_per_function(extracted, keys_order, trx_map)
    statistics_df = build_statistics_sheet(matrices)
    write_excel(functions_df, statistics_df, matrices)

if __name__ == "__main__":
    main()
