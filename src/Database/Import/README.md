# Tradebook workbook import

This directory contains the controlled import pipeline for the five workbooks named in the authoritative entity model. The workbooks themselves are intentionally not stored in this repository.

Create a Python 3.13 environment and install `requirements.txt`, then validate all sources before connecting to PostgreSQL:

```powershell
python src/Database/Import/import_tradebook.py --validate-only `
  --workbook masterdata=C:\secure\Tradebook_Masterdata.xlsx `
  --workbook certificates=C:\secure\Tradebook_Certificates_v2.xlsx `
  --workbook physical=C:\secure\Tradebook_Physical.xlsm `
  --workbook bioticket=C:\secure\Tradebook_Bioticket.xlsx `
  --workbook reports=C:\secure\Tradebook_Reports_V2_SF_integration.xlsx
```

After validation, pass the PostgreSQL connection string through `--database-url`. The importer holds a transaction-scoped advisory lock, validates all mapped database identifiers against `public`, resolves foreign keys exactly once, rejects duplicate business keys, and applies every table merge in one transaction. Any validation or database error rolls back the entire run.

The mapping is deliberately fail-closed. If an operational workbook renamed a sheet or header after the entity model was captured, update and review `mapping.yaml`; do not add fuzzy database-column inference to the importer.
