from __future__ import annotations

import os
import sys
import unittest
from datetime import date
from decimal import Decimal
from pathlib import Path
from uuid import uuid4

from openpyxl import Workbook

sys.path.insert(0, str(Path(__file__).parent))

from import_tradebook import (  # noqa: E402
    ImportContractError,
    discover_header,
    extract_rows,
    load_manifest,
    merge_records,
    normalize_header,
    psycopg,
    resolve_vat_fallbacks,
    sql,
    summarize_record_counts,
    transform,
)


class ImportTradebookTests(unittest.TestCase):
    def test_repository_manifest_is_valid_and_only_uses_declared_tables(self) -> None:
        manifest = load_manifest(Path(__file__).with_name("mapping.yaml"))

        self.assertEqual(1, manifest["version"])
        self.assertEqual(
            {"masterdata", "certificates", "physical", "bioticket", "reports"},
            set(manifest["workbooks"]),
        )
        self.assertTrue({mapping["table"] for mapping in manifest["mappings"]} <= set(manifest["allowed_tables"]))

    def test_header_matching_is_case_and_punctuation_insensitive(self) -> None:
        workbook = Workbook()
        sheet = workbook.active
        sheet.append(["decorative title"])
        sheet.append(["Contract ID", "Supply-Month", "Volume [MWh]"])
        sheet.append(["C-1", date(2026, 8, 15), "12,50"])
        columns = {
            "contract": {"sources": ["contract id"], "transform": "text", "required": True},
            "month": {"sources": ["Supply Month"], "transform": "month", "required": True},
            "volume": {"sources": ["Volume MWh"], "transform": "decimal"},
        }

        header_row, positions = discover_header(sheet, columns, location="test")
        rows = extract_rows(sheet, columns, location="test")

        self.assertEqual(2, header_row)
        self.assertEqual({"contract": 0, "month": 1, "volume": 2}, positions)
        self.assertEqual(date(2026, 8, 1), rows[0]["month"])
        self.assertEqual("12.50", str(rows[0]["volume"]))

    def test_missing_required_header_fails_closed(self) -> None:
        workbook = Workbook()
        sheet = workbook.active
        sheet.append(["Unrelated"])
        columns = {"contract": {"sources": ["Contract ID"], "transform": "text", "required": True}}

        with self.assertRaisesRegex(ImportContractError, "missing required"):
            discover_header(sheet, columns, location="test")

    def test_invalid_boolean_and_identifier_are_rejected(self) -> None:
        with self.assertRaisesRegex(ImportContractError, "expected a boolean"):
            transform("sometimes", "boolean", location="test")
        self.assertEqual("eursekmwh", normalize_header(" EUR/SEK [MWh] "))

    def test_validate_only_counts_sum_repeated_target_mappings(self) -> None:
        extracted = [
            ({"table": "physical_deliveries"}, [{}, {}], "physical/sourcing"),
            ({"table": "physical_deliveries"}, [{}], "physical/sales"),
            ({"table": "market_prices"}, [{}, {}, {}], "physical/prices"),
            ({"table": "market_prices"}, [{}], "certificates/prices"),
        ]

        self.assertEqual(
            {"physical_deliveries": 3, "market_prices": 4},
            summarize_record_counts(extracted),
        )


@unittest.skipUnless(
    os.environ.get("TRADEBOOK_TEST_DATABASE_URL") and psycopg is not None,
    "set TRADEBOOK_TEST_DATABASE_URL and install psycopg to run PostgreSQL importer coverage",
)
class ImportTradebookPostgresTests(unittest.TestCase):
    def test_blank_book_vat_uses_the_counterparty_country_rate_or_zero_when_not_applicable(self) -> None:
        schema = f"importer_vat_{uuid4().hex[:12]}"
        applicable_counterparty = uuid4()
        exempt_counterparty = uuid4()
        applicable_contract = uuid4()
        exempt_contract = uuid4()
        assert psycopg is not None
        assert sql is not None

        with psycopg.connect(os.environ["TRADEBOOK_TEST_DATABASE_URL"]) as connection:
            try:
                with connection.cursor() as cursor:
                    cursor.execute(sql.SQL("CREATE SCHEMA {schema}").format(schema=sql.Identifier(schema)))
                    cursor.execute(
                        sql.SQL(
                            "CREATE TABLE {schema}.companies ("
                            "id uuid PRIMARY KEY, country_code char(2), vat_rate numeric(5,4))"
                        ).format(schema=sql.Identifier(schema))
                    )
                    cursor.execute(
                        sql.SQL(
                            "CREATE TABLE {schema}.counterparties ("
                            "id uuid PRIMARY KEY, country_code char(2), vat_applicable boolean NOT NULL)"
                        ).format(schema=sql.Identifier(schema))
                    )
                    cursor.execute(
                        sql.SQL(
                            "CREATE TABLE {schema}.contracts ("
                            "id uuid PRIMARY KEY, counterparty_id uuid NOT NULL)"
                        ).format(schema=sql.Identifier(schema))
                    )
                    cursor.execute(
                        sql.SQL("SET search_path TO {schema}").format(schema=sql.Identifier(schema))
                    )
                    cursor.execute(
                        "INSERT INTO companies (id, country_code, vat_rate) VALUES (%s, 'DK', 0.25)",
                        (uuid4(),),
                    )
                    cursor.executemany(
                        "INSERT INTO counterparties (id, country_code, vat_applicable) VALUES (%s, %s, %s)",
                        [
                            (applicable_counterparty, "DK", True),
                            (exempt_counterparty, "SE", False),
                        ],
                    )
                    cursor.executemany(
                        "INSERT INTO contracts (id, counterparty_id) VALUES (%s, %s)",
                        [
                            (applicable_contract, applicable_counterparty),
                            (exempt_contract, exempt_counterparty),
                        ],
                    )

                records = [
                    {"contract_id": applicable_contract, "vat_pct": None},
                    {"contract_id": exempt_contract},
                    {"contract_id": applicable_contract, "vat_pct": Decimal("0.10")},
                ]
                resolve_vat_fallbacks(connection, records, location="vat-test")

                self.assertEqual(Decimal("0.2500"), records[0]["vat_pct"])
                self.assertEqual(Decimal("0"), records[1]["vat_pct"])
                self.assertEqual(Decimal("0.10"), records[2]["vat_pct"])
            finally:
                connection.rollback()
                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL("DROP SCHEMA IF EXISTS {schema} CASCADE").format(
                            schema=sql.Identifier(schema)
                        )
                    )
                connection.commit()

    def test_vat_fallback_rejects_an_ambiguous_country_rate(self) -> None:
        schema = f"importer_vat_{uuid4().hex[:12]}"
        counterparty_id = uuid4()
        contract_id = uuid4()
        assert psycopg is not None
        assert sql is not None

        with psycopg.connect(os.environ["TRADEBOOK_TEST_DATABASE_URL"]) as connection:
            try:
                with connection.cursor() as cursor:
                    cursor.execute(sql.SQL("CREATE SCHEMA {schema}").format(schema=sql.Identifier(schema)))
                    cursor.execute(
                        sql.SQL(
                            "CREATE TABLE {schema}.companies ("
                            "id uuid PRIMARY KEY, country_code char(2), vat_rate numeric(5,4)); "
                            "CREATE TABLE {schema}.counterparties ("
                            "id uuid PRIMARY KEY, country_code char(2), vat_applicable boolean NOT NULL); "
                            "CREATE TABLE {schema}.contracts ("
                            "id uuid PRIMARY KEY, counterparty_id uuid NOT NULL)"
                        ).format(schema=sql.Identifier(schema))
                    )
                    cursor.execute(
                        sql.SQL("SET search_path TO {schema}").format(schema=sql.Identifier(schema))
                    )
                    cursor.executemany(
                        "INSERT INTO companies (id, country_code, vat_rate) VALUES (%s, 'DK', %s)",
                        [(uuid4(), Decimal("0.25")), (uuid4(), Decimal("0.20"))],
                    )
                    cursor.execute(
                        "INSERT INTO counterparties (id, country_code, vat_applicable) VALUES (%s, 'DK', true)",
                        (counterparty_id,),
                    )
                    cursor.execute(
                        "INSERT INTO contracts (id, counterparty_id) VALUES (%s, %s)",
                        (contract_id, counterparty_id),
                    )

                with self.assertRaisesRegex(ImportContractError, "2 distinct VAT rates"):
                    resolve_vat_fallbacks(
                        connection,
                        [{"contract_id": contract_id, "vat_pct": None}],
                        location="vat-test",
                    )
            finally:
                connection.rollback()
                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL("DROP SCHEMA IF EXISTS {schema} CASCADE").format(
                            schema=sql.Identifier(schema)
                        )
                    )
                connection.commit()

    def test_repeated_target_mappings_reuse_stage_and_identical_rows_do_not_bump_version(self) -> None:
        table = f"importer_merge_{uuid4().hex[:12]}"
        assert psycopg is not None
        assert sql is not None

        with psycopg.connect(os.environ["TRADEBOOK_TEST_DATABASE_URL"]) as connection:
            try:
                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL(
                            "CREATE TABLE {table} ("
                            "business_key text PRIMARY KEY, payload text, "
                            "version bigint NOT NULL DEFAULT 1, "
                            "updated_at timestamptz NOT NULL DEFAULT clock_timestamp())"
                        ).format(table=sql.Identifier(table))
                    )
                connection.commit()

                # These calls deliberately share one transaction and one target,
                # matching the repeated physical_deliveries/market_prices mappings.
                with connection.transaction():
                    merge_records(
                        connection,
                        table,
                        [{"business_key": "A", "payload": "first"}],
                        ["business_key"],
                    )
                    merge_records(
                        connection,
                        table,
                        [{"business_key": "B", "payload": "second"}],
                        ["business_key"],
                    )
                    merge_records(
                        connection,
                        table,
                        [{"business_key": "A", "payload": "first"}],
                        ["business_key"],
                    )

                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL(
                            "SELECT business_key, payload, version FROM {table} ORDER BY business_key"
                        ).format(table=sql.Identifier(table))
                    )
                    self.assertEqual(
                        [("A", "first", 1), ("B", "second", 1)],
                        cursor.fetchall(),
                    )

                with connection.transaction():
                    merge_records(
                        connection,
                        table,
                        [{"business_key": "A", "payload": "changed"}],
                        ["business_key"],
                    )
                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL(
                            "SELECT payload, version FROM {table} WHERE business_key = 'A'"
                        ).format(table=sql.Identifier(table))
                    )
                    self.assertEqual(("changed", 2), cursor.fetchone())
            finally:
                connection.rollback()
                with connection.cursor() as cursor:
                    cursor.execute(
                        sql.SQL("DROP TABLE IF EXISTS {table}").format(table=sql.Identifier(table))
                    )
                connection.commit()


if __name__ == "__main__":
    unittest.main()
