import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { z } from 'zod';
import type { GetDeliveryHistoryResponse } from "../../api/generated/types.gen";
import type { PhysicalDeliveryDetailsDto } from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import {
  useCreateDelivery,
  useDeleteDelivery,
  useUpdateDelivery,
  type CreateDeliveryVariables,
  type UpdateDeliveryVariables,
} from "../../lib/mutations/entityMutations";
import { queryKeys } from '../../lib/query/queryKeys';
import { useContractOptions } from '../../lib/query/useContractOptions';
import { useUiStore } from '../../lib/state/useUiStore';
import { isMoneyString, moneyInputField, normalizeMoneyInput } from '../../lib/validation/money-input';
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
import { Button } from "../ui/button";
import { Combobox } from '../ui/combobox';
import { EntityCreateDrawer } from '../ui/entity-create-drawer';
import { Frame, FrameDescription, FrameHeader, FramePanel, FrameTitle } from '../ui/frame';
import { Input } from '../ui/input';
import { NumberInput } from '../ui/number-input';
import { RecordDetailPanel } from '../ui/record-detail-panel';
import { RecordActivity } from '../ui/record-activity';
import { TableEditableCell } from '../ui/table-editable-cell';
import { Select } from '../ui/select';
import { ValidatedForm } from '../ui/validated-form';

const statuses = [
  "Completed - Payment Received/Sent",
  "In Progress - Invoice Received/Sent",
  "Pending - No Invoice",
  "Cancelled",
  "Awaiting",
  "Issue",
] as const;
interface ConflictState {
  id: string;
  serverState?: PhysicalDeliveryDetailsDto;
  attempted: object;
}

const initialCreate: CreateDeliveryVariables = {
  contractId: "",
  bookType: "Sourcing",
  supplyMonth: new Date().toISOString().slice(0, 7) + "-01",
};
// Mirrors the generated zCreatePhysicalDeliveryRequest: volume fields are Money STRINGS on
// the wire; moneyInputField normalizes the raw input text into that contract.
const createDeliverySchema: z.ZodType<CreateDeliveryVariables> = z.object({
  contractId: z.string().trim().min(1, { error: 'Contract ID is required.' }),
  contractInstanceId: z.string().nullish(),
  bookType: z.string(),
  supplyMonth: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, { error: 'Enter the supply month as a full day (YYYY-MM-DD).' }),
  capacityMw: moneyInputField({ label: 'Capacity MW' }),
  volumeNominatedMwh: moneyInputField({ label: 'Nominated volume MWh' }),
  volumeRealisedMwh: moneyInputField({ label: 'Realised volume MWh', nonnegative: true }),
  priceMechanism: z.string().nullish(),
  startDay: z.string().nullish(),
  endDay: z.string().nullish(),
});

function matchesSearch(delivery: PhysicalDeliveryDetailsDto, term: string): boolean {
  if (term === '') return true;
  const lowered = term.toLowerCase();
  const candidates: Array<unknown> = [
    delivery.deliveryId,
    delivery.contractId,
    delivery.contractInstanceId,
    delivery.bookType,
    delivery.supplyMonth,
    delivery.status,
  ];
  return candidates.some((value) => String(value ?? '').toLowerCase().includes(lowered));
}

export function DeliveriesPage() {
  const [page, setPage] = useState(1);
  const showCreate = useUiStore((state) => state.activeModal === 'create-delivery');
  const openCreate = useUiStore((state) => state.openModal);
  const closeCreate = useUiStore((state) => state.closeModal);
  const [createRequest, setCreateRequest] =
    useState<CreateDeliveryVariables>(initialCreate);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState<ConflictState>();
  const [search, setSearch] = useState("");
  const [selectedRowIds, setSelectedRowIds] = useState<Set<string>>(new Set());
  const [activeDelivery, setActiveDelivery] = useState<PhysicalDeliveryDetailsDto>();
  const contractOptions = useContractOptions();
  const [panelDeliveryVolume, setPanelDeliveryVolume] = useState("");
  const [panelDeliveryStatus, setPanelDeliveryStatus] = useState<(typeof statuses)[number]>(statuses[0]);
  const attempted = useRef<object>({});
  const commandStack = useCommandStack();
  const [, setHistoryRevision] = useState(0);
  const onConflict = useCallback(
    (id: string, serverState?: PhysicalDeliveryDetailsDto) =>
      setConflict({ id, serverState, attempted: attempted.current }),
    [],
  );
  const onMutationError = useCallback(
    (failure: unknown) =>
      setError(
        failure instanceof Error
          ? failure.message
          : "The change could not be saved.",
      ),
    [],
  );
  const createMutation = useCreateDelivery(onMutationError);
  const updateMutation = useUpdateDelivery(onConflict, onMutationError);
  const deleteMutation = useDeleteDelivery(onConflict, onMutationError);
  const history = useQuery({
    queryKey: queryKeys.deliveries.list({ page, pageSize: 100 }),
    queryFn: ({ signal }) => apiFetch<GetDeliveryHistoryResponse>(
      `/api/v1/deliveries?page=${page}&pageSize=100`,
      { signal },
    ),
  });

  const deliveries = useMemo(() => history.data?.items ?? [], [history.data?.items]);
  const searchableDeliveries = useMemo(() => {
    const term = search.trim();
    return deliveries.filter((delivery) => matchesSearch(delivery, term.toLowerCase()));
  }, [deliveries, search]);
  const selectedCount = selectedRowIds.size;
  const totalCount = history.data?.totalCount ?? 0;
  const setSelection = useCallback((nextSelectedRowIds: Set<string>) => {
    setSelectedRowIds(nextSelectedRowIds);
  }, []);
  const refreshHistory = useCallback(
    () => setHistoryRevision((value) => value + 1),
    [],
  );
  const undo = useCallback(async () => {
    if (!(await commandStack.undo()))
      setError("Nothing could be undone. The server may have a newer version.");
    refreshHistory();
  }, [commandStack, refreshHistory]);
  const redo = useCallback(async () => {
    try {
      if (!(await commandStack.redo())) setError("Nothing to redo.");
    } catch (failure) {
      onMutationError(failure);
    }
    refreshHistory();
  }, [commandStack, onMutationError, refreshHistory]);

  const saveDelivery = useCallback(
    async (
      delivery: PhysicalDeliveryDetailsDto,
      requested: UpdateDeliveryVariables["changes"],
    ) => {
      setError("");
      // The row editor passes the raw input text; the wire contract wants a Money string.
      const volumeRealisedMwh = typeof requested.volumeRealisedMwh === 'string'
        ? normalizeMoneyInput(requested.volumeRealisedMwh)
        : requested.volumeRealisedMwh;
      if (typeof volumeRealisedMwh === 'string' && !isMoneyString(volumeRealisedMwh)) {
        setError("Realised volume MWh must be a decimal number (for example 12.5).");
        return;
      }
      const changes = { ...requested, volumeRealisedMwh };
      attempted.current = changes;
      let version = delivery.version;
      const before = {
        volumeRealisedMwh: delivery.volumeRealisedMwh,
        status: delivery.status,
      } as UpdateDeliveryVariables["changes"];
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Update ${delivery.contractInstanceId}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = changes;
          const updated = await updateMutation.mutateAsync({
            id: delivery.deliveryId,
            version,
            changes,
          });
          version = updated.version;
        },
        undo: async () => {
          attempted.current = before;
          const updated = await updateMutation.mutateAsync({
            id: delivery.deliveryId,
            version,
            changes: before,
          });
          version = updated.version;
        },
      };
      try {
        await commandStack.execute(command);
        refreshHistory();
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, refreshHistory, updateMutation],
  );

  const cancelDelivery = useCallback(
    async (delivery: PhysicalDeliveryDetailsDto) => {
      const previousStatus = delivery.status;
      let version = delivery.version;
      const reason = "Cancelled from Tradebook UI";
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Cancel ${delivery.contractInstanceId}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = { status: "Cancelled", reason };
          await deleteMutation.mutateAsync({
            id: delivery.deliveryId,
            reason,
            version,
          });
          const current = await apiFetch<PhysicalDeliveryDetailsDto>(
            `/api/v1/deliveries/${delivery.deliveryId}`,
          );
          version = current.version;
        },
        undo: async () => {
          const changes = {
            status: previousStatus,
            volumeRealisedMwh: delivery.volumeRealisedMwh,
          } as UpdateDeliveryVariables["changes"];
          attempted.current = changes;
          const restored = await updateMutation.mutateAsync({
            id: delivery.deliveryId,
            version,
            changes,
          });
          version = restored.version;
        },
      };
      try {
        await commandStack.execute(command);
        refreshHistory();
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, deleteMutation, refreshHistory, updateMutation],
  );

  const columns = useMemo<ColumnDef<PhysicalDeliveryDetailsDto>[]>(
    () => [
      {
        accessorKey: "contractInstanceId",
        header: "Contract instance",
        cell: ({ row }) => (
          <Button
            intent="ghost"
            type="button"
            onClick={() => setActiveDelivery(row.original)}
            aria-label={`Open delivery ${row.original.contractInstanceId}`}
          >
            {row.original.contractInstanceId}
          </Button>
        ),
      },
      { accessorKey: "bookType", header: "Book", cell: ({ row }) => <TableEditableCell label="Book" readOnly value={row.original.bookType} /> },
      { accessorKey: "supplyMonth", header: "Supply month", cell: ({ row }) => <TableEditableCell label="Supply month" readOnly value={row.original.supplyMonth} /> },
      {
        accessorKey: "volumeRealisedMwh",
        header: "Realised MWh",
        cell: ({ row }) => <TableEditableCell kind="number" label="Realised volume MWh" value={String(row.original.volumeRealisedMwh ?? '')} onCommit={(value) => saveDelivery(row.original, { volumeRealisedMwh: value, status: row.original.status })} />,
      },
      {
        accessorKey: "invoiceAmountEur",
        header: "Invoice EUR",
        cell: ({ getValue }) => <TableEditableCell label="Invoice EUR" readOnly value={String(getValue() ?? "—")} />,
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => <TableEditableCell label="Status" options={statuses} value={row.original.status} onCommit={(value) => saveDelivery(row.original, { volumeRealisedMwh: row.original.volumeRealisedMwh, status: value })} />,
      },
    ],
    [saveDelivery],
  );

  const submitCreate = async (validatedRequest: CreateDeliveryVariables) => {
    setError("");
    let createdId = "";
    let version = 0;
    let createdStatus = "Pending - No Invoice";
    const request = { ...validatedRequest };
    const command: Command = {
      id: crypto.randomUUID(),
      description: `Create ${request.contractInstanceId ?? request.contractId}`,
      timestamp: Date.now(),
      execute: async () => {
        attempted.current = request;
        if (!createdId) {
          const created = await createMutation.mutateAsync(request);
          createdId = created.deliveryId;
          version = created.version;
          createdStatus = created.status;
          return;
        }
        const restored = await updateMutation.mutateAsync({
          id: createdId,
          version,
          changes: {
            status: createdStatus,
            volumeRealisedMwh: request.volumeRealisedMwh,
          },
        });
        version = restored.version;
      },
      undo: async () => {
        attempted.current = { status: "Cancelled", reason: "Undo create" };
        await deleteMutation.mutateAsync({
          id: createdId,
          reason: "Undo create",
          version,
        });
        version = (
          await apiFetch<PhysicalDeliveryDetailsDto>(
            `/api/v1/deliveries/${createdId}`,
          )
        ).version;
      },
    };
    try {
      await commandStack.execute(command);
      closeCreate();
      setCreateRequest(initialCreate);
      refreshHistory();
    } catch {
      /* mutation callbacks surface the error */
    }
  };

  useEffect(() => {
    if (!activeDelivery) return;
    const refreshed = deliveries.find((delivery) => delivery.deliveryId === activeDelivery.deliveryId);
    if (!refreshed) return;
    setActiveDelivery(refreshed);
    setPanelDeliveryVolume(refreshed.volumeRealisedMwh?.toString() ?? "");
    setPanelDeliveryStatus(refreshed.status as (typeof statuses)[number]);
  }, [activeDelivery, deliveries]);

  const submitPanelSave = useCallback(async () => {
    if (!activeDelivery) return;
    const normalized = panelDeliveryVolume === "" ? undefined : normalizeMoneyInput(panelDeliveryVolume);
    if (typeof normalized === 'string' && !isMoneyString(normalized)) {
      setError("Realised volume MWh must be a decimal number (for example 12.5).");
      return;
    }
    await saveDelivery(activeDelivery, { status: panelDeliveryStatus, volumeRealisedMwh: normalized });
  }, [activeDelivery, panelDeliveryStatus, panelDeliveryVolume, saveDelivery]);

  const submitPanelCancel = useCallback(async () => {
    if (!activeDelivery) return;
    await cancelDelivery(activeDelivery);
  }, [activeDelivery, cancelDelivery]);

  const panelDirty = Boolean(activeDelivery) && (
    panelDeliveryVolume !== (activeDelivery?.volumeRealisedMwh?.toString() ?? '')
    || panelDeliveryStatus !== activeDelivery?.status
  );

  return (
    <section>
      <header className="mb-6 flex items-start justify-between gap-4 max-[800px]:flex-col max-[800px]:items-stretch">
        <div>
          <p className="eyebrow">Operations</p>
          <h2>Deliveries</h2>
          <p>
            {history.data
              ? `${history.data.totalCount} records`
              : "Loading delivery history…"}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            onClick={() => void undo()}
            disabled={!commandStack.canUndo}
          >
            Undo
          </Button>
          <Button
            type="button"
            onClick={() => void redo()}
            disabled={!commandStack.canRedo}
          >
            Redo
          </Button>
          <Button
            data-testid="btn-create-delivery"
            type="button"
            onClick={() => openCreate('create-delivery')}
          >
            New delivery
          </Button>
        </div>
      </header>
      <section className="toolbar" aria-label="Delivery list toolbar">
        <label>
          Search deliveries
          <Input
            aria-label="Search deliveries"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            type="search"
            placeholder="Search delivery, contract, status..."
          />
        </label>
        <div aria-label="Selection">
          {selectedCount} selected
        </div>
        <div aria-label="Record count">
          {searchableDeliveries.length} of {totalCount} deliveries
        </div>
      </section>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {history.isError && <p role="alert">Unable to load delivery history.</p>}
      {!history.isError && (
        <VirtualizedDataTable
          testId="virtual-deliveries-grid"
          data={searchableDeliveries}
          columns={columns}
          ariaLabel="Deliveries"
          getRowId={(row) => row.deliveryId}
          onRowOpen={setActiveDelivery}
          selectedRowIds={selectedRowIds}
          onSelectedRowIdsChange={setSelection}
        />
      )}
      {history.data && (
        <nav className="flex flex-wrap items-center gap-2" aria-label="Delivery history pages">
          <button type="button" disabled={page === 1 || history.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button>
          <span>Page {page}</span>
          <button type="button" disabled={!history.data.hasNextPage || history.isFetching} onClick={() => setPage((value) => value + 1)}>Next</button>
        </nav>
      )}
      <EntityCreateDrawer
        description="Add a delivery and connect it to an existing contract."
        onOpenChange={(open) => { if (!open) closeCreate(); }}
        open={showCreate}
        title="Create physical delivery"
      >
          <ValidatedForm schema={createDeliverySchema} values={createRequest} onValid={submitCreate}>
            <Combobox
              disabled={contractOptions.isLoading}
              label="Contract"
              options={contractOptions.options}
              placeholder={contractOptions.isLoading ? 'Loading contracts…' : 'Search contracts…'}
              value={createRequest.contractId}
              onChange={(contractId) =>
                setCreateRequest((value) => ({ ...value, contractId }))
              }
            />
            {contractOptions.isError && (
              <p role="alert">Contracts could not be loaded. Close this form and try again.</p>
            )}
            <label>
              Contract instance (optional)
              <Input
                value={createRequest.contractInstanceId ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    contractInstanceId: event.target.value || undefined,
                  }))
                }
              />
            </label>
            <div data-slot="entity-create-field">
              <span>Book type</span>
              <Select
                label="Book type"
                options={['Sourcing', 'Sales', 'Intercompany']}
                value={createRequest.bookType}
                onValueChange={(bookType) => setCreateRequest((value) => ({ ...value, bookType }))}
              />
            </div>
            <label>
              Supply month
              <Input
                required
                type="date"
                value={createRequest.supplyMonth}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    supplyMonth: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              Realised volume MWh
              <NumberInput
                aria-label="Realised volume MWh"
                min={0}
                value={createRequest.volumeRealisedMwh ?? ""}
                onValueChange={(nextValue) =>
                  setCreateRequest((current) => ({
                    ...current,
                    volumeRealisedMwh:
                      nextValue === "" ? undefined : nextValue,
                  }))
                }
              />
            </label>
            <div data-slot="entity-create-drawer-actions" className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="secondary"
                onClick={closeCreate}
              >
                Close
              </button>
              <button type="submit" disabled={createMutation.isPending}>
                Create
              </button>
            </div>
          </ValidatedForm>
      </EntityCreateDrawer>
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
      {activeDelivery && (
        <RecordDetailPanel
          open
          onOpenChange={(open) => {
            if (!open) setActiveDelivery(undefined);
          }}
          eyebrow="Delivery"
          title={activeDelivery.contractInstanceId || activeDelivery.contractId}
          description={`${activeDelivery.bookType} delivery for ${activeDelivery.supplyMonth}`}
          recordId={activeDelivery.deliveryId}
          version={activeDelivery.version}
          dirty={panelDirty}
          properties={(
            <div data-slot="record-field-grid">
              <div data-slot="record-field">
                <span>Status</span>
                <Select
                  label="Delivery status"
                  options={[...statuses]}
                  value={panelDeliveryStatus}
                  onValueChange={(value) => setPanelDeliveryStatus(value as (typeof statuses)[number])}
                />
              </div>
              <label data-slot="record-field">
                <span>Realised volume</span>
                <div data-slot="input-with-unit">
                  <NumberInput
                    aria-label="Volume realised MWh"
                    value={panelDeliveryVolume}
                    onValueChange={setPanelDeliveryVolume}
                    min={0}
                  />
                  <span>MWh</span>
                </div>
              </label>
            </div>
          )}
          context={(
            <>
              <Frame>
                <FrameHeader>
                  <FrameTitle>Contract</FrameTitle>
                  <FrameDescription>Relationship attached to this delivery.</FrameDescription>
                </FrameHeader>
                <FramePanel>
                  <dl data-slot="record-facts">
                    <div><dt>Instance</dt><dd>{activeDelivery.contractInstanceId || '—'}</dd></div>
                    <div><dt>Contract</dt><dd>{activeDelivery.contractId}</dd></div>
                    <div><dt>Book</dt><dd>{activeDelivery.bookType}</dd></div>
                  </dl>
                </FramePanel>
              </Frame>
              <Frame>
                <FrameHeader>
                  <FrameTitle>Commercial summary</FrameTitle>
                  <FrameDescription>Read-only values calculated by Tradebook.</FrameDescription>
                </FrameHeader>
                <FramePanel>
                  <dl data-slot="record-facts">
                    <div><dt>Nominated</dt><dd>{activeDelivery.volumeNominatedMwh ?? '—'} MWh</dd></div>
                    <div><dt>Capacity</dt><dd>{activeDelivery.capacityMw ?? '—'} MW</dd></div>
                    <div><dt>Invoice</dt><dd>{activeDelivery.invoiceAmountEur ?? '—'} EUR</dd></div>
                  </dl>
                </FramePanel>
              </Frame>
            </>
          )}
          activity={<RecordActivity entityId={activeDelivery.deliveryId} entityName="physical_deliveries" />}
          actions={(
            <>
              <Button aria-label="Close panel" intent="secondary" type="button" onClick={() => setActiveDelivery(undefined)}>Close</Button>
              <Button aria-label="Cancel" intent="danger" type="button" onClick={() => void submitPanelCancel()} disabled={activeDelivery.status === 'Cancelled'}>Cancel delivery</Button>
              <Button aria-label="Save" type="button" onClick={() => void submitPanelSave()} disabled={updateMutation.isPending}>Save changes</Button>
            </>
          )}
        />
      )}
    </section>
  );
}
