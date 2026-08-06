import os
import re
import json
import yaml
import sys

RESEARCH_DIR = r"c:\Users\LaxmananKrishnapilla\tradebook\research"
FILES = [
    "versioning-and-audit-trails.md",
    "semantic-modeling-and-data-sources.md",
    "snappy-crud-ui-ux.md",
    "custom-visualizations.md"
]

results = {
    "json_schemas": [],
    "yaml_files": [],
    "sql_blocks": [],
    "surrealql_blocks": [],
    "protobuf_blocks": [],
    "typescript_blocks": [],
    "csharp_blocks": []
}

def extract_code_blocks(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # Pattern for code blocks ```lang ... ```
    pattern = r"```([a-zA-Z0-9_\-\+]*)\n(.*?)```"
    blocks = re.findall(pattern, content, re.DOTALL)
    return blocks

print("=== STARTING EXTRACTING & VERIFYING SCHEMAS ===")

for filename in FILES:
    filepath = os.path.join(RESEARCH_DIR, filename)
    blocks = extract_code_blocks(filepath)
    print(f"\nFile: {filename} - Found {len(blocks)} code blocks.")
    
    for i, (lang, code) in enumerate(blocks):
        lang_lower = lang.lower().strip()
        
        # 1. JSON
        if lang_lower == "json":
            try:
                parsed = json.loads(code)
                # Check if it's a JSON Schema
                if isinstance(parsed, dict) and "$schema" in parsed:
                    results["json_schemas"].append((filename, i, True, parsed.get("title", "Untitled Schema"), None))
                else:
                    results["json_schemas"].append((filename, i, True, "JSON Data/AST Payload", None))
            except Exception as e:
                results["json_schemas"].append((filename, i, False, "JSON Parse Error", str(e)))

        # 2. YAML
        elif lang_lower in ["yaml", "yml"]:
            try:
                parsed = yaml.safe_load(code)
                results["yaml_files"].append((filename, i, True, "YAML Semantic Model", None))
            except Exception as e:
                results["yaml_files"].append((filename, i, False, "YAML Parse Error", str(e)))

        # 3. SQL
        elif lang_lower == "sql":
            results["sql_blocks"].append((filename, i, code))

        # 4. SurrealQL
        elif lang_lower == "surrealql":
            results["surrealql_blocks"].append((filename, i, code))

        # 5. Protobuf
        elif lang_lower in ["protobuf", "proto"]:
            results["protobuf_blocks"].append((filename, i, code))

        # 6. TypeScript / JS
        elif lang_lower in ["typescript", "ts"]:
            results["typescript_blocks"].append((filename, i, code))

        # 7. C#
        elif lang_lower in ["csharp", "cs"]:
            results["csharp_blocks"].append((filename, i, code))


print("\n--- JSON SCHEMAS & PAYLOADS VERIFICATION ---")
for fn, idx, status, title, err in results["json_schemas"]:
    print(f"[{'PASS' if status else 'FAIL'}] {fn} (block #{idx}): {title}")
    if err:
        print(f"   Error: {err}")

# Try importing jsonschema to validate schemas formally
try:
    import jsonschema
    print("\n--- FORMAL JSON SCHEMA DRAFT-07 VALIDATION ---")
    for fn, idx, status, title, err in results["json_schemas"]:
        if status:
            filepath = os.path.join(RESEARCH_DIR, fn)
            blocks = extract_code_blocks(filepath)
            code = blocks[idx][1]
            parsed = json.loads(code)
            if isinstance(parsed, dict) and "$schema" in parsed:
                try:
                    jsonschema.Draft7Validator.check_schema(parsed)
                    print(f"[PASS] Draft7Validator checked schema '{title}' in {fn}")
                except Exception as ve:
                    print(f"[FAIL] Draft7Validator schema error in '{title}' in {fn}: {ve}")
except ImportError:
    print("jsonschema module not installed, skipped formal Draft7Validator check.")

print("\n--- YAML MODEL VERIFICATION ---")
for fn, idx, status, title, err in results["yaml_files"]:
    print(f"[{'PASS' if status else 'FAIL'}] {fn} (block #{idx}): {title}")
    if err:
        print(f"   Error: {err}")

print(f"\nExtracted: SQL={len(results['sql_blocks'])}, SurrealQL={len(results['surrealql_blocks'])}, Protobuf={len(results['protobuf_blocks'])}, TS={len(results['typescript_blocks'])}, C#={len(results['csharp_blocks'])}")
