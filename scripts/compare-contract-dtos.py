#!/usr/bin/env python3
"""Fail when a public C# transport DTO and its TypeSpec model have different fields."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
tsp_text = (root / "docs/api/typespec/models/domain.tsp").read_text(encoding="utf-8")
tsp_models = {
    name: set(re.findall(r"^\s{2}(\w+)\??:", body, re.MULTILINE))
    for name, body in re.findall(r"^model (\w+) \{(.*?)^\}", tsp_text, re.MULTILINE | re.DOTALL)
}
errors: list[str] = []
for path in sorted((root / "src/Backend/src/Tradebook.Core/DTOs").glob("*.cs")):
    source = path.read_text(encoding="utf-8-sig")
    record = re.search(r"public sealed record (\w+)", source)
    if not record:
        continue
    name = record.group(1)
    properties = {
        property_name[0].lower() + property_name[1:]
        for property_name in re.findall(
            r"^\s*public\s+(?:required\s+)?[\w<>,.?\[\]]+\s+(\w+)\s*\{\s*get;",
            source,
            re.MULTILINE,
        )
    }
    contract = tsp_models.get(name)
    if contract is None:
        errors.append(f"{name}: missing TypeSpec model")
    elif properties != contract:
        errors.append(
            f"{name}: C# only={sorted(properties - contract)}, TypeSpec only={sorted(contract - properties)}"
        )

unimplemented = sorted(set(tsp_models) - {
    re.search(r"public sealed record (\w+)", p.read_text(encoding="utf-8-sig")).group(1)
    for p in (root / "src/Backend/src/Tradebook.Core/DTOs").glob("*.cs")
    if re.search(r"public sealed record (\w+)", p.read_text(encoding="utf-8-sig"))
})
if unimplemented:
    errors.append(f"TypeSpec models without C# DTOs: {unimplemented}")
if errors:
    print("Contract DTO drift detected:", *errors, sep="\n  - ", file=sys.stderr)
    sys.exit(1)
print(f"C# and TypeSpec DTO fields agree for {len(tsp_models)} models.")
