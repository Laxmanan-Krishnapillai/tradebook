import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { z } from "zod";
import type { GetMarketPriceHistoryResponse } from "../../api/generated/types.gen";
import type { MarketPriceDetailsDto } from "../../api/generated/types.gen";
import type { UpsertMarketPriceRequest } from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import { changedFields, draftValuesEquivalent, shouldAdoptRefreshedDraft } from "../../lib/editor/detailDraftPolicy";
import { clearMutationConflictForEntity } from "../../lib/mutations/mutationCoordinator";
import {
    useDeleteMarketPrice,
    useUpsertMarketPrice,
} from "../../lib/mutations/domainEntityMutations";
import { queryKeys } from "../../lib/query/queryKeys";
import {
    moneyInputField,
} from "../../lib/validation/money-input";
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
import { Button } from "../ui/button";
import { EntityCreateDrawer } from "../ui/entity-create-drawer";
import {
    Frame,
    FrameDescription,
    FrameHeader,
    FramePanel,
    FrameTitle,
} from "../ui/frame";
import { Input } from "../ui/input";
import { NumberInput } from "../ui/number-input";
import { RecordDetailPanel } from "../ui/record-detail-panel";
import { RecordActivity } from "../ui/record-activity";
import { TableEditableCell } from "../ui/table-editable-cell";
import { ValidatedForm } from "../ui/validated-form";

interface ConflictState {
    id: string;
    serverState?: MarketPriceDetailsDto;
    attempted: object;
}

function asRecord(value: object): Record<string, unknown> {
    return value as Record<string, unknown>;
}

const today = () => new Date().toISOString().slice(0, 10);
const initialCreate = (): UpsertMarketPriceRequest => ({
    priceDate: today(),
    ttfEurMwh: "0",
    version: 0,
});
// Mirrors the generated zUpsertMarketPriceRequest: every price field is a Money STRING on
// the wire; moneyInputField normalizes the raw input text into that contract.
const upsertMarketPriceSchema: z.ZodType<UpsertMarketPriceRequest> = z.object({
    priceDate: z
        .string()
        .regex(/^\d{4}-\d{2}-\d{2}$/, {
            error: "Enter the market date as a full day (YYYY-MM-DD).",
        }),
    ttfEurMwh: moneyInputField({ label: "TTF EUR/MWh", required: true }),
    egsiEtfEurMwh: moneyInputField({ label: "EGSI ETF EUR/MWh" }),
    theEurMwh: moneyInputField({ label: "THE EUR/MWh" }),
    bgoEurMwh: moneyInputField({ label: "BGO EUR/MWh" }),
    pgoEurMwh: moneyInputField({ label: "PGO EUR/MWh" }),
    euaEurMwh: moneyInputField({ label: "EUA EUR/MWh" }),
    withinDayMktEurMwh: moneyInputField({ label: "Within-day EUR/MWh" }),
    eurSek: moneyInputField({ label: "EUR/SEK" }),
    eurChf: moneyInputField({ label: "EUR/CHF" }),
    eurGbp: moneyInputField({ label: "EUR/GBP" }),
    eurUsd: moneyInputField({ label: "EUR/USD", positive: true }),
    eurDkk: moneyInputField({ label: "EUR/DKK" }),
    version: z.int().min(0),
});

function marketRecordToPriceRequest(
    price: MarketPriceDetailsDto,
    ttfEurMwh: string,
): UpsertMarketPriceRequest {
    return {
        priceDate: price.priceDate,
        ttfEurMwh,
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
function displayPriceValue(value: unknown): string {
    return value === null || value === undefined || value === ""
        ? "â€”"
        : String(value);
}
function matchesSearch(price: MarketPriceDetailsDto, term: string): boolean {
    const lowered = term.trim().toLowerCase();
    if (!lowered) return true;
    return [
        price.priceDate,
        String(price.ttfEurMwh ?? ""),
        String(price.egsiEtfEurMwh ?? ""),
        String(price.euaEurMwh ?? ""),
        String(price.eurUsd ?? ""),
    ].some((candidate) => candidate.toLowerCase().includes(lowered));
}
const marketPriceFields = [
    { key: "ttfEurMwh", label: "TTF EUR/MWh" },
    { key: "egsiEtfEurMwh", label: "EGSI ETF EUR/MWh" },
    { key: "theEurMwh", label: "THE EUR/MWh" },
    { key: "bgoEurMwh", label: "BGO EUR/MWh" },
    { key: "pgoEurMwh", label: "PGO EUR/MWh" },
    { key: "euaEurMwh", label: "EUA EUR/MWh" },
    { key: "withinDayMktEurMwh", label: "Within-day EUR/MWh" },
    { key: "eurSek", label: "EUR/SEK" },
    { key: "eurChf", label: "EUR/CHF" },
    { key: "eurGbp", label: "EUR/GBP" },
    { key: "eurUsd", label: "EUR/USD" },
    { key: "eurDkk", label: "EUR/DKK" },
] as const;
export function MarketPricesPage() {
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState("");
    const [showCreate, setShowCreate] = useState(false);
    const [selectedRowIds, setSelectedRowIds] = useState<Set<string>>(
        new Set(),
    );
    const [createRequest, setCreateRequest] =
        useState<UpsertMarketPriceRequest>(initialCreate);
    const [activePrice, setActivePrice] = useState<MarketPriceDetailsDto>();
    const [activeRequest, setActiveRequest] =
        useState<UpsertMarketPriceRequest>();
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
            apiFetch<GetMarketPriceHistoryResponse>(
                `/api/v1/market-prices?page=${page}&pageSize=100`,
                { signal },
            ),
    });

    const prices = useMemo(
        () => history.data?.items ?? [],
        [history.data?.items],
    );
    const searchablePrices = useMemo(
        () => prices.filter((price) => matchesSearch(price, search)),
        [prices, search],
    );
    const totalCount = history.data?.totalCount ?? 0;

    useEffect(() => {
        if (!activePrice || !activeRequest) return;
        const refreshed = prices.find((price) => price.priceDate === activePrice.priceDate);
        if (!refreshed) return;
        const activeValues = marketRecordToPriceRequest(activePrice, activePrice.ttfEurMwh ?? "0");
        const refreshedValues = marketRecordToPriceRequest(refreshed, refreshed.ttfEurMwh ?? "0");
        const valuesMatchDraft = (values: UpsertMarketPriceRequest) => marketPriceFields.every((field) => (
            draftValuesEquivalent(values[field.key], activeRequest[field.key])
        ));
        const dirty = !valuesMatchDraft(activeValues);
        const refreshedMatchesDraft = valuesMatchDraft(refreshedValues);
        if (!shouldAdoptRefreshedDraft({
            activeVersion: activePrice.version,
            refreshedVersion: refreshed.version,
            dirty,
            refreshedMatchesDraft,
        })) return;
        setActivePrice(refreshed);
        setActiveRequest(refreshedValues);
    }, [activePrice, activeRequest, prices]);

    const openPricePanel = useCallback((price: MarketPriceDetailsDto) => {
        setActivePrice(price);
        setActiveRequest(
            marketRecordToPriceRequest(price, price.ttfEurMwh ?? "0"),
        );
    }, []);
    const closePricePanel = useCallback(() => {
        setActivePrice(undefined);
        setActiveRequest(undefined);
    }, []);
    const onSelectedRowIdsChange = useCallback((next: Set<string>) => {
        setSelectedRowIds(next);
    }, []);

    const save = useCallback(
        async (
            price: MarketPriceDetailsDto,
            requested: UpsertMarketPriceRequest,
        ) => {
            setError("");
            const parsed = upsertMarketPriceSchema.safeParse(requested);
            if (!parsed.success) {
                setError(parsed.error.issues[0]?.message ?? "Enter valid market-price values.");
                return;
            }
            const request = parsed.data;
            attempted.current = request;
            let version = price.version;
            const before = marketRecordToPriceRequest(
                price,
                price.ttfEurMwh ?? "0",
            );
            const intent = changedFields(before, request).filter((key) => key !== 'version');
            const command: Command = {
                id: crypto.randomUUID(),
                description: `Update market price ${price.priceDate}`,
                timestamp: Date.now(),
                execute: async () => {
                    attempted.current = request;
                    version = (
                        await upsertMutation.mutateAsync({
                            ...request,
                            version,
                            intent,
                        })
                    ).version;
                },
                undo: async () => {
                    attempted.current = before;
                    version = (
                        await upsertMutation.mutateAsync({ ...before, version, intent })
                    ).version;
                },
            };
            try {
                await commandStack.execute(command);
                setActiveRequest(
                    marketRecordToPriceRequest(price, request.ttfEurMwh ?? "0"),
                );
            } catch {
                // mutation callbacks surface the error.
            }
        },
        [commandStack, upsertMutation],
    );

    const remove = useCallback(
        async (price: MarketPriceDetailsDto) => {
            const reason = "Deleted from Tradebook UI";
            attempted.current = { reason };
            let version = price.version;
            const snapshot = marketRecordToPriceRequest(
                price,
                price.ttfEurMwh ?? "0",
            );
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
                        await upsertMutation.mutateAsync({
                            ...snapshot,
                            version: 0,
                        })
                    ).version;
                },
            };
            try {
                await commandStack.execute(command);
                closePricePanel();
            } catch {
                // mutation callbacks surface the error.
            }
        },
        [closePricePanel, commandStack, deleteMutation, upsertMutation],
    );

    const submitPanelSave = useCallback(async () => {
        if (!activePrice || !activeRequest) return;
        await save(activePrice, activeRequest);
    }, [activePrice, activeRequest, save]);

    const submitPanelDelete = useCallback(async () => {
        if (!activePrice) return;
        await remove(activePrice);
    }, [activePrice, remove]);

    const panelDirty = activePrice && activeRequest
        ? marketPriceFields.some((field) => {
            const original = marketRecordToPriceRequest(activePrice, activePrice.ttfEurMwh ?? "0");
            const edited = activeRequest;
            const current = edited[field.key as keyof UpsertMarketPriceRequest];
            const before = original[field.key as keyof UpsertMarketPriceRequest];
            return !draftValuesEquivalent(current, before);
        })
        : false;

    const panelContext = activePrice ? (
        <Frame>
            <FrameHeader>
                <FrameTitle>Price details</FrameTitle>
                <FrameDescription>Read-only data from this snapshot.</FrameDescription>
            </FrameHeader>
            <FramePanel>
                <dl data-slot="record-facts">
                    <div>
                        <dt>Market date</dt>
                        <dd>{activePrice.priceDate}</dd>
                    </div>
                    <div>
                        <dt>Version</dt>
                        <dd>{activePrice.version}</dd>
                    </div>
                    {asRecord(activePrice).createdAt !== undefined && (
                        <div>
                            <dt>Created</dt>
                            <dd>{displayPriceValue(asRecord(activePrice).createdAt)}</dd>
                        </div>
                    )}
                    {asRecord(activePrice).updatedAt !== undefined && (
                        <div>
                            <dt>Updated</dt>
                            <dd>{displayPriceValue(asRecord(activePrice).updatedAt)}</dd>
                        </div>
                    )}
                </dl>
            </FramePanel>
        </Frame>
    ) : null;

    const panelActivity = activePrice
        ? <RecordActivity entityId={activePrice.priceDate} entityName="market_prices" />
        : null;

    const savePriceField = useCallback(async (
        price: MarketPriceDetailsDto,
        key: keyof UpsertMarketPriceRequest,
        value: string,
    ) => {
        await save(price, {
            ...marketRecordToPriceRequest(price, price.ttfEurMwh ?? "0"),
            [key]: value,
        });
    }, [save]);

    const columns = useMemo<ColumnDef<MarketPriceDetailsDto>[]>(
        () => [
            {
                accessorKey: "priceDate",
                header: "Date",
                cell: ({ row }) => (
                    <Button
                        aria-label={`Open market price ${row.original.priceDate}`}
                        intent="ghost"
                        title="Date is read-only; open record details"
                        onClick={() => openPricePanel(row.original)}
                    >
                        {row.original.priceDate}
                    </Button>
                ),
            },
            {
                accessorKey: "ttfEurMwh",
                header: "TTF EUR/MWh",
                cell: ({ row }) => <TableEditableCell kind="number" label="TTF EUR/MWh" value={String(row.original.ttfEurMwh ?? '')} onCommit={(value) => savePriceField(row.original, 'ttfEurMwh', value)} />,
            },
            {
                accessorKey: "egsiEtfEurMwh",
                header: "EGSI ETF",
                cell: ({ row }) => <TableEditableCell kind="number" label="EGSI ETF" value={String(row.original.egsiEtfEurMwh ?? '')} onCommit={(value) => savePriceField(row.original, 'egsiEtfEurMwh', value)} />,
            },
            {
                accessorKey: "euaEurMwh",
                header: "EUA",
                cell: ({ row }) => <TableEditableCell kind="number" label="EUA" value={String(row.original.euaEurMwh ?? '')} onCommit={(value) => savePriceField(row.original, 'euaEurMwh', value)} />,
            },
            {
                accessorKey: "eurUsd",
                header: "EUR/USD",
                cell: ({ row }) => <TableEditableCell kind="number" label="EUR/USD" value={String(row.original.eurUsd ?? '')} onCommit={(value) => savePriceField(row.original, 'eurUsd', value)} />,
            },
        ],
        [openPricePanel, savePriceField],
    );

    const submitCreate = async (validatedRequest: UpsertMarketPriceRequest) => {
        setError("");
        attempted.current = validatedRequest;
        try {
            const request = { ...validatedRequest, version: 0 };
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
            // mutation callbacks surface the error.
        }
    };

    return (
        <section>
            <header className="mb-6 flex items-start justify-between gap-4 max-[800px]:flex-col max-[800px]:items-stretch">
                <div>
                    <p className="eyebrow">Market data</p>
                    <h2>Market prices</h2>
                    <p>
                        {history.data
                            ? `${history.data.totalCount} daily observations`
                            : "Loading market-price history…"}
                    </p>
                </div>
            </header>
            <section className="toolbar" aria-label="Market-price list toolbar">
                <label>
                    Search market prices
                    <Input
                        type="search"
                        aria-label="Search market prices"
                        placeholder="Search by date or price"
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                    />
                </label>
                <div aria-label="Selection">{selectedRowIds.size} selected</div>
                <div aria-label="Record count">
                    {searchablePrices.length} of {totalCount} daily observations
                </div>
                <Button
                    type="button"
                    onClick={() => setShowCreate(true)}
                    data-testid="btn-create-market-price"
                >
                    Add daily price
                </Button>
            </section>
            {error && (
                <p role="alert" className="error-banner">
                    {error}
                </p>
            )}
            {history.isError && (
                <p role="alert">Unable to load market prices.</p>
            )}
            {!history.isError && (
                <VirtualizedDataTable
                    testId="virtual-market-prices-grid"
                    data={searchablePrices}
                    columns={columns}
                    getRowId={(row) => row.priceDate}
                    onRowOpen={openPricePanel}
                    selectedRowIds={selectedRowIds}
                    onSelectedRowIdsChange={onSelectedRowIdsChange}
                />
            )}
            {history.data && (
                <nav
                    className="flex flex-wrap items-center gap-2"
                    aria-label="Market-price history pages"
                >
                    <Button
                        intent="secondary"
                        size="sm"
                        type="button"
                        disabled={page === 1 || history.isFetching}
                        onClick={() =>
                            setPage((value) => Math.max(1, value - 1))
                        }
                    >
                        Previous
                    </Button>
                    <span>Page {page}</span>
                    <Button
                        intent="secondary"
                        size="sm"
                        type="button"
                        disabled={
                            !history.data.hasNextPage || history.isFetching
                        }
                        onClick={() => setPage((value) => value + 1)}
                    >
                        Next
                    </Button>
                </nav>
            )}
            <EntityCreateDrawer
                description="Add the daily reference price and optional FX rate."
                onOpenChange={setShowCreate}
                open={showCreate}
                title="Add daily market price"
            >
                    <ValidatedForm
                        schema={upsertMarketPriceSchema}
                        values={createRequest}
                        onValid={submitCreate}
                    >
                        <label>
                            Date
                            <Input
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
                            <NumberInput
                                aria-label="TTF EUR/MWh"
                                required
                                value={createRequest.ttfEurMwh ?? ""}
                                onValueChange={(nextValue) =>
                                    setCreateRequest((value) => ({
                                        ...value,
                                        ttfEurMwh:
                                            nextValue === ""
                                                ? null
                                                : nextValue,
                                    }))
                                }
                            />
                        </label>
                        <label>
                            EUR/USD
                            <NumberInput
                                aria-label="EUR/USD"
                                min={0}
                                value={createRequest.eurUsd ?? ""}
                                onValueChange={(nextValue) =>
                                    setCreateRequest((value) => ({
                                        ...value,
                                        eurUsd:
                                            nextValue === ""
                                                ? null
                                                : nextValue,
                                    }))
                                }
                            />
                        </label>
                        <div data-slot="entity-create-drawer-actions" className="flex flex-wrap items-center gap-2">
                            <Button
                                intent="secondary"
                                type="button"
                                onClick={() => setShowCreate(false)}
                            >
                                Close
                            </Button>
                            <Button
                                type="submit"
                                disabled={upsertMutation.isPending}
                            >
                                Save
                            </Button>
                        </div>
                    </ValidatedForm>
            </EntityCreateDrawer>
            {activePrice && activeRequest && (
                <RecordDetailPanel
                    open={Boolean(activePrice)}
                    onOpenChange={(open) => {
                        if (!open) closePricePanel();
                    }}
                    eyebrow="Market"
                    title={`Update market price ${activePrice.priceDate}`}
                    description="Update the TTF price and optional related fields."
                    recordId={activePrice.priceDate}
                    version={activePrice.version}
                    dirty={panelDirty}
                    properties={(
                        <div data-slot="record-field-grid">
                            {marketPriceFields.map((field) => (
                                <label key={field.key} data-slot="record-field">
                                    <span>{field.label}</span>
                                    <NumberInput
                                        aria-label={field.label}
                                        value={
                                            (activeRequest?.[
                                                field.key as keyof UpsertMarketPriceRequest
                                            ] as string | null) ?? ""
                                        }
                                        onValueChange={(nextValue) =>
                                            setActiveRequest((value) => ({
                                                ...(value ?? initialCreate()),
                                                [field.key]:
                                                    nextValue === ""
                                                        ? null
                                                        : nextValue,
                                            }))
                                        }
                                    />
                                </label>
                            ))}
                        </div>
                    )}
                    context={panelContext}
                    activity={panelActivity}
                    actions={(
                        <>
                            <Button
                                type="button"
                                onClick={() => void submitPanelSave()}
                            >
                                Save
                            </Button>
                            <Button
                                intent="danger"
                                type="button"
                                onClick={() => void submitPanelDelete()}
                            >
                                Delete
                            </Button>
                        </>
                    )}
                />
            )}
            {conflict && (
                <div className="modal">
                    <ConflictDialog
                        entityId={conflict.id}
                        serverState={conflict.serverState}
                        attemptedChanges={conflict.attempted}
                        onClose={() => {
                            clearMutationConflictForEntity(conflict.id);
                            setConflict(undefined);
                        }}
                    />
                </div>
            )}
        </section>
    );
}
