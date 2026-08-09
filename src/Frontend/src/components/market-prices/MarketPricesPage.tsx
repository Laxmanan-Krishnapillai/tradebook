import Decimal from "decimal.js";
import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { type FormEvent, useCallback, useMemo, useRef, useState } from "react";
import type { GetMarketPriceHistoryResponse } from "../../api/generated/types.gen";
import type { MarketPriceDetailsDto } from "../../api/generated/types.gen";
import type { UpsertMarketPriceRequest } from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import {
  useDeleteMarketPrice,
  useUpsertMarketPrice,
} from "../../lib/mutations/domainEntityMutations";
import { queryKeys } from '../../lib/query/queryKeys';
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";

interface ConflictState {
  id: string;
  serverState?: MarketPriceDetailsDto;
  attempted: object;
}
const today = () => new Date().toISOString().slice(0, 10);
const initialCreate = (): UpsertMarketPriceRequest => ({
  priceDate: today(),
  ttfEurMwh: "0",
  version: 0,
});

function currentRequest(
  price: MarketPriceDetailsDto,
  ttfEurMwh: string,
): UpsertMarketPriceRequest {
  return {
    priceDate: price.priceDate,
    ttfEurMwh: new Decimal(ttfEurMwh).toString(),
    egsiEtfEurMwh: price.egsiEtfEurMwh,
    theEurMwh: price.theEurMwh,
    bgoEurMwh: price.bgoEurMwh,
    pgoEurMwh: price.pgoEurMwh,
    euaEurMwh: price.euaEurMwh,
    withinDayMktEurMwh: price.withinDayMktEurMwh,
    eurSek: price.eurSek,
    eurChf: price.eurChf,
    eurGbp: price.eurGbp,
    eurUsd: price.eurUsd,
    eurDkk: price.eurDkk,
    version: price.version,
  };
}

function MarketPriceEditor({
  price,
  onSave,
  onDelete,
}: {
  price: MarketPriceDetailsDto;
  onSave: (
    price: MarketPriceDetailsDto,
    request: UpsertMarketPriceRequest,
  ) => void;
  onDelete: (price: MarketPriceDetailsDto) => void;
}) {
  const [ttf, setTtf] = useState(price.ttfEurMwh?.toString() ?? "");
  return (
    <div className="row-actions">
      <input
        aria-label={`TTF price for ${price.priceDate}`}
        type="number"
        step="any"
        value={ttf}
        onChange={(event) => setTtf(event.target.value)}
      />
      <button
        type="button"
        disabled={ttf === ""}
        onClick={() => onSave(price, currentRequest(price, ttf))}
      >
        Save
      </button>
      <button type="button" className="danger" onClick={() => onDelete(price)}>
        Delete
      </button>
    </div>
  );
}

export function MarketPricesPage() {
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [createRequest, setCreateRequest] =
    useState<UpsertMarketPriceRequest>(initialCreate);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState<ConflictState>();
  const attempted = useRef<object>({});
  const commandStack = useCommandStack();
  const onConflict = useCallback(
    (id: string, serverState?: MarketPriceDetailsDto) =>
      setConflict({ id, serverState, attempted: attempted.current }),
    [],
  );
  const onError = useCallback(
    (failure: unknown) =>
      setError(
        failure instanceof Error
          ? failure.message
          : "The market-price change could not be saved.",
      ),
    [],
  );
  const upsertMutation = useUpsertMarketPrice(onConflict, onError);
  const deleteMutation = useDeleteMarketPrice(onConflict, onError);
  const history = useQuery({
    queryKey: queryKeys.marketPrices.list({ page, pageSize: 100 }),
    queryFn: ({ signal }) =>
      apiFetch<GetMarketPriceHistoryResponse>(`/api/v1/market-prices?page=${page}&pageSize=100`, { signal }),
  });

  const save = useCallback(
    async (price: MarketPriceDetailsDto, request: UpsertMarketPriceRequest) => {
      setError("");
      attempted.current = request;
      let version = price.version;
      const before = currentRequest(price, price.ttfEurMwh ?? "0");
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Update market price ${price.priceDate}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = request;
          version = (await upsertMutation.mutateAsync({ ...request, version }))
            .version;
        },
        undo: async () => {
          attempted.current = before;
          version = (await upsertMutation.mutateAsync({ ...before, version }))
            .version;
        },
      };
      try {
        await commandStack.execute(command);
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, upsertMutation],
  );

  const remove = useCallback(
    async (price: MarketPriceDetailsDto) => {
      const reason = "Deleted from Tradebook UI";
      attempted.current = { reason };
      let version = price.version;
      const snapshot = currentRequest(price, price.ttfEurMwh ?? "0");
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Delete market price ${price.priceDate}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = { reason };
          await deleteMutation.mutateAsync({
            id: price.priceDate,
            version,
            reason,
          });
        },
        undo: async () => {
          attempted.current = snapshot;
          version = (
            await upsertMutation.mutateAsync({ ...snapshot, version: 0 })
          ).version;
        },
      };
      try {
        await commandStack.execute(command);
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, deleteMutation, upsertMutation],
  );

  const columns = useMemo<ColumnDef<MarketPriceDetailsDto>[]>(
    () => [
      { accessorKey: "priceDate", header: "Date" },
      {
        accessorKey: "ttfEurMwh",
        header: "TTF EUR/MWh",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        accessorKey: "egsiEtfEurMwh",
        header: "EGSI ETF",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        accessorKey: "euaEurMwh",
        header: "EUA",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        accessorKey: "eurUsd",
        header: "EUR/USD",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        id: "edit",
        header: "Edit",
        cell: ({ row }) => (
          <MarketPriceEditor
            key={row.original.version}
            price={row.original}
            onSave={(price, request) => void save(price, request)}
            onDelete={(price) => void remove(price)}
          />
        ),
      },
    ],
    [remove, save],
  );

  const submitCreate = async (event: FormEvent) => {
    event.preventDefault();
    setError("");
    attempted.current = createRequest;
    try {
      const request = { ...createRequest, version: 0 };
      let version = 0;
      let created = false;
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Create market price ${request.priceDate}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = request;
          version = (
            await upsertMutation.mutateAsync({
              ...request,
              version: created ? 0 : version,
            })
          ).version;
          created = true;
        },
        undo: async () => {
          await deleteMutation.mutateAsync({
            id: request.priceDate,
            version,
            reason: "Undo create",
          });
          version = 0;
        },
      };
      await commandStack.execute(command);
      setCreateRequest(initialCreate());
      setShowCreate(false);
    } catch {
      /* mutation callbacks surface the error */
    }
  };

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="eyebrow">Market data</p>
          <h2>Market prices</h2>
          <p>
            {history.data
              ? `${history.data.totalCount} daily observations`
              : "Loading market-price history…"}
          </p>
        </div>
        <button
          data-testid="btn-create-market-price"
          type="button"
          onClick={() => setShowCreate(true)}
        >
          Add daily price
        </button>
      </header>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {history.isError && <p role="alert">Unable to load market prices.</p>}
      {!history.isError && (
        <VirtualizedDataTable
          testId="virtual-market-prices-grid"
          data={history.data?.items ?? []}
          columns={columns}
          getRowId={(row) => row.priceDate}
        />
      )}
      {history.data && (
        <nav className="toolbar" aria-label="Market-price history pages">
          <button type="button" disabled={page === 1 || history.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button>
          <span>Page {page}</span>
          <button type="button" disabled={!history.data.hasNextPage || history.isFetching} onClick={() => setPage((value) => value + 1)}>Next</button>
        </nav>
      )}
      {showCreate && (
        <section
          className="modal"
          role="dialog"
          aria-modal="true"
          aria-label="Add market price"
        >
          <form onSubmit={(event) => void submitCreate(event)}>
            <h3>Add daily market price</h3>
            <label>
              Date
              <input
                required
                type="date"
                value={createRequest.priceDate}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    priceDate: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              TTF EUR/MWh
              <input
                required
                type="number"
                step="any"
                value={createRequest.ttfEurMwh ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    ttfEurMwh:
                      event.target.value === ""
                        ? null
                        : event.target.value,
                  }))
                }
              />
            </label>
            <label>
              EUR/USD
              <input
                type="number"
                min="0"
                step="any"
                value={createRequest.eurUsd ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    eurUsd:
                      event.target.value === ""
                        ? null
                        : event.target.value,
                  }))
                }
              />
            </label>
            <div className="toolbar">
              <button
                type="button"
                className="secondary"
                onClick={() => setShowCreate(false)}
              >
                Close
              </button>
              <button type="submit" disabled={upsertMutation.isPending}>
                Save
              </button>
            </div>
          </form>
        </section>
      )}
      {conflict && (
        <div className="modal">
          <ConflictDialog
            entityId={conflict.id}
            serverState={conflict.serverState}
            attemptedChanges={conflict.attempted}
            onClose={() => setConflict(undefined)}
          />
        </div>
      )}
    </section>
  );
}
