import math

# CRS Model Parameters from R1 Section 3
categories = [
    {"name": "Operational Overhead (S_op)", "weight": 0.25, "base_score": 92, "alt_score": 28},
    {"name": "Team Expertise & Hiring (S_dev)", "weight": 0.20, "base_score": 85, "alt_score": 25},
    {"name": "Infrastructure Cost (S_cost)", "weight": 0.20, "base_score": 88, "alt_score": 30},
    {"name": "Cognitive Load (S_cog)", "weight": 0.20, "base_score": 90, "alt_score": 32},
    {"name": "Failure Surface (S_fail)", "weight": 0.15, "base_score": 94, "alt_score": 35},
]

print("=== 1. WEIGHTS SUM CHECK ===")
total_weight = sum(c["weight"] for c in categories)
print(f"Total Weight: {total_weight:.4f} (Expected: 1.0000)")
assert math.isclose(total_weight, 1.0), "Weights do not sum to 1.0!"

print("\n=== 2. BASELINE SCORE CALCULATION ===")
c_base_terms = [c["weight"] * c["base_score"] for c in categories]
for c, term in zip(categories, c_base_terms):
    print(f"  {c['name']}: {c['weight']} * {c['base_score']} = {term:.4f}")
c_base_calc = sum(c_base_terms)
print(f"Calculated C_base: {c_base_calc:.4f}")
print(f"R1 Document C_base: 89.70")
assert math.isclose(c_base_calc, 89.70), f"C_base mismatch! Calc: {c_base_calc}, Doc: 89.70"

print("\n=== 3. ALTERNATIVE SCORE CALCULATION ===")
c_alt_terms = [c["weight"] * c["alt_score"] for c in categories]
for c, term in zip(categories, c_alt_terms):
    print(f"  {c['name']}: {c['weight']} * {c['alt_score']} = {term:.4f}")
c_alt_calc = sum(c_alt_terms)
print(f"Calculated C_alt: {c_alt_calc:.4f}")
print(f"R1 Document C_alt: 29.65")
assert math.isclose(c_alt_calc, 29.65), f"C_alt mismatch! Calc: {c_alt_calc}, Doc: 29.65"

print("\n=== 4. CRS FORMULA VERIFICATION ===")
raw_diff = c_base_calc - c_alt_calc
crs_exact = (raw_diff / c_base_calc) * 100
print(f"Raw Difference (C_base - C_alt): {raw_diff:.4f}")
print(f"Exact CRS Percentage: {crs_exact:.8f}%")
print(f"CRS rounded to 2 decimal places (standard rounding): {round(crs_exact, 2)}%")
print(f"CRS truncated to 2 decimal places: {math.floor(crs_exact * 100) / 100}%")
print(f"R1 Document CRS: 66.94%")

print("\n=== 5. SENSITIVITY & ASSUMPTION ANALYSIS ===")
# Sensitivity test: What if category weights are equal (20% each)?
c_base_equal = sum(0.20 * c["base_score"] for c in categories)
c_alt_equal = sum(0.20 * c["alt_score"] for c in categories)
crs_equal = ((c_base_equal - c_alt_equal) / c_base_equal) * 100
print(f"Equal Weights (20% each) -> C_base: {c_base_equal:.2f}, C_alt: {c_alt_equal:.2f}, CRS: {crs_equal:.2f}%")

# Sensitivity test: What if Failure Surface is weighted higher (30%) and Op Overhead (10%)?
weights_alt1 = [0.10, 0.20, 0.20, 0.20, 0.30]
c_base_alt1 = sum(w * c["base_score"] for w, c in zip(weights_alt1, categories))
c_alt_alt1 = sum(w * c["alt_score"] for w, c in zip(weights_alt1, categories))
crs_alt1 = ((c_base_alt1 - c_alt_alt1) / c_base_alt1) * 100
print(f"Alt Weights (Op 10%, Fail 30%) -> C_base: {c_base_alt1:.2f}, C_alt: {c_alt_alt1:.2f}, CRS: {crs_alt1:.2f}%")
