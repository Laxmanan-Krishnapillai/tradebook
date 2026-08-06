import json
import math

def test_bitemporal_exclusion_logic():
    print("=== TEST 1: Bi-Temporal Exclusion Constraint Logic in SQL DDL ===")
    # In R1 DDL:
    # EXCLUDE USING gist (tenant_id WITH =, entity_name WITH =, entity_id WITH =, system_time WITH &&, valid_time WITH &&)
    # system_time DEFAULT tstzrange(clock_timestamp(), NULL, '[)')
    
    # Record 1 at T1=10:00:00
    rec1 = {
        "tenant_id": "tenant-1",
        "entity_name": "trade",
        "entity_id": "trade-100",
        "system_time": (1000, float('inf')), # [10:00, inf)
        "valid_time": (1000, float('inf'))   # [10:00, inf)
    }
    
    # Record 2 (Update/Revert) at T2=10:05:00
    rec2 = {
        "tenant_id": "tenant-1",
        "entity_name": "trade",
        "entity_id": "trade-100",
        "system_time": (1005, float('inf')), # [10:05, inf)
        "valid_time": (1000, float('inf'))   # [10:00, inf)
    }
    
    # Check overlap:
    sys_overlap = max(rec1["system_time"][0], rec2["system_time"][0]) < min(rec1["system_time"][1], rec2["system_time"][1])
    val_overlap = max(rec1["valid_time"][0], rec2["valid_time"][0]) < min(rec1["valid_time"][1], rec2["valid_time"][1])
    
    exclusion_violated = (
        rec1["tenant_id"] == rec2["tenant_id"] and
        rec1["entity_name"] == rec2["entity_name"] and
        rec1["entity_id"] == rec2["entity_id"] and
        sys_overlap and val_overlap
    )
    
    print(f"Record 1 system_time: [10:00, inf), valid_time: [10:00, inf)")
    print(f"Record 2 system_time: [10:05, inf), valid_time: [10:00, inf)")
    print(f"System time overlap (&&): {sys_overlap}")
    print(f"Valid time overlap (&&): {val_overlap}")
    print(f"PostgreSQL Exclusion Constraint Violated? -> {exclusion_violated}")
    print("CRITICAL FINDING: Subsequent updates to audit_log for the same entity will FAIL in PostgreSQL unless system_time upper bound is explicitly closed first!")

def test_go_float_precision():
    print("\n=== TEST 2: Go Float64 Financial Precision Loss ===")
    price = 19.99
    quantity = 1000.03
    total_float = price * quantity
    print(f"Float64 calculation: {price} * {quantity} = {total_float:.17f}")
    # Fixed point representation (e.g. cents * units)
    price_cents = 1999
    qty_units = 100003 # 1000.03 * 100
    total_fixed = (price_cents * qty_units) / 10000.0
    print(f"Exact fixed calculation: {total_fixed:.4f}")
    diff = abs(total_float - total_fixed)
    print(f"Floating point IEEE-754 discrepancy: {diff:.17f}")

def test_r1_vs_r2_metric_contradictions():
    print("\n=== TEST 3: Cross-Document Metric Contradictions (R1 vs R2) ===")
    r1_lightweight_tps = 25000 # R1 Section 5 Table 5
    r2_lightweight_tps_max = 12000 # R2 Section 2 Table Stack 4
    r2_lightweight_tps_min = 3000
    
    print(f"R1 Lightweight Stack Write Throughput claim: {r1_lightweight_tps:,} ops/sec")
    print(f"R2 Lightweight Hybrid Scale Limit claim: {r2_lightweight_tps_min:,} - {r2_lightweight_tps_max:,} TPS")
    print(f"Contradiction Factor: R1 claims {r1_lightweight_tps / r2_lightweight_tps_max:.2f}x higher throughput than R2's maximum bound!")

if __name__ == "__main__":
    test_bitemporal_exclusion_logic()
    test_go_float_precision()
    test_r1_vs_r2_metric_contradictions()
