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
import { useUiStore } from '../../lib/state/useUiStore';
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
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

function DeliveryEditor({
  delivery,
  onSave,
  onCancel,
}: {
  delivery: PhysicalDeliveryDetailsDto;
  onSave: (
    delivery: PhysicalDeliveryDetailsDto,
    changes: UpdateDeliveryVariables["changes"],
  ) => void;
  onCancel: (delivery: PhysicalDeliveryDetailsDto) => void;
}) {
  const [volume, setVolume] = useState(
    delivery.volumeRealisedMwh?.toString() ?? "",
  );
  const [status, setStatus] = useState(delivery.status);
  useEffect(() => {
    setVolume(delivery.volumeRealisedMwh?.toString() ?? "");
    setStatus(delivery.status);
  }, [delivery.status, delivery.volumeRealisedMwh]);
  return (
    <div className="row-actions">
      <input
        data-testid={`delivery-volume-${delivery.deliveryId}`}
        aria-label={`Realised volume for ${delivery.contractInstanceId}`}
        type="number"
        min="0"
        step="any"
        value={volume}
        onChange={(event) => setVolume(event.target.value)}
      />
      <select
        aria-label={`Status for ${delivery.contractInstanceId}`}
        value={status}
        onChange={(event) => setStatus(event.target.value)}
      >
        {statuses.map((value) => (
          <option key={value}>{value}</option>
        ))}
      </select>
      <button
        data-testid={`btn-save-${delivery.deliveryId}`}
        type="button"
        onClick={() =>
          onSave(delivery, {
            volumeRealisedMwh: volume === "" ? undefined : Number(volume),
            status,
          } as UpdateDeliveryVariables["changes"])
        }
      >
        Save
      </button>
      <button
        data-testid={`btn-cancel-${delivery.deliveryId}`}
        type="button"
        className="danger"
        disabled={delivery.status === "Cancelled"}
        onClick={() => onCancel(delivery)}
      >
        Cancel
      </button>
    </div>
  );
}

const initialCreate: CreateDeliveryVariables = {
  contractId: "",
  bookType: "Sourcing",
  supplyMonth: new Date().toISOString().slice(0, 7) + "-01",
};
const createDeliverySchema = z.custom<CreateDeliveryVariables>((candidate): candidate is CreateDeliveryVariables => {
  const value = candidate as Partial<CreateDeliveryVariables>;
  return (
  typeof value.contractId === 'string' && value.contractId.trim().length > 0
  && typeof value.supplyMonth === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value.supplyMonth)
  && (value.volumeRealisedMwh === undefined || (typeof value.volumeRealisedMwh === 'number' && Number.isFinite(value.volumeRealisedMwh) && value.volumeRealisedMwh >= 0))
  );
}, { error: 'Complete the delivery with valid values.' });

export function DeliveriesPage() {
  const [page, setPage] = useState(1);
  const showCreate = useUiStore((state) => state.activeModal === 'create-delivery');
  const openCreate = useUiStore((state) => state.openModal);
  const closeCreate = useUiStore((state) => state.closeModal);
  const [createRequest, setCreateRequest] =
    useState<CreateDeliveryVariables>(initialCreate);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState<ConflictState>();
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
      changes: UpdateDeliveryVariables["changes"],
    ) => {
      setError("");
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
      { accessorKey: "contractInstanceId", header: "Contract instance" },
      { accessorKey: "bookType", header: "Book" },
      { accessorKey: "supplyMonth", header: "Supply month" },
      {
        accessorKey: "volumeMwh",
        header: "Volume MWh",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        accessorKey: "invoiceAmountEur",
        header: "Invoice EUR",
        cell: ({ getValue }) => String(getValue() ?? "—"),
      },
      {
        id: "edit",
        header: "Edit",
        cell: ({ row }) => (
          <DeliveryEditor
            delivery={row.original}
            onSave={(delivery, changes) => void saveDelivery(delivery, changes)}
            onCancel={(delivery) => void cancelDelivery(delivery)}
          />
        ),
      },
    ],
    [cancelDelivery, saveDelivery],
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

  const deliveries = history.data?.items ?? [];
  return (
    <section>
      <header className="page-header">
        <div>
          <p className="eyebrow">Operations</p>
          <h2>Physical deliveries</h2>
          <p>
            {history.data
              ? `${history.data.totalCount} records`
              : "Loading delivery history…"}
          </p>
        </div>
        <div className="toolbar">
          <button
            type="button"
            onClick={() => void undo()}
            disabled={!commandStack.canUndo}
          >
            Undo
          </button>
          <button
            type="button"
            onClick={() => void redo()}
            disabled={!commandStack.canRedo}
          >
            Redo
          </button>
          <button
            data-testid="btn-create-delivery"
            type="button"
            onClick={() => openCreate('create-delivery')}
          >
            Create delivery
          </button>
        </div>
      </header>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {history.isError && <p role="alert">Unable to load delivery history.</p>}
      {!history.isError && (
        <VirtualizedDataTable
          testId="virtual-deliveries-grid"
          data={deliveries}
          columns={columns}
          getRowId={(row) => row.deliveryId}
        />
      )}
      {history.data && (
        <nav className="toolbar" aria-label="Delivery history pages">
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
          aria-label="Create physical delivery"
        >
          <ValidatedForm schema={createDeliverySchema} values={createRequest} onValid={submitCreate}>
            <h3>Create physical delivery</h3>
            <label>
              Contract ID
              <input
                required
                value={createRequest.contractId}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    contractId: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              Contract instance (optional)
              <input
                value={createRequest.contractInstanceId ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    contractInstanceId: event.target.value || undefined,
                  }))
                }
              />
            </label>
            <label>
              Book type
              <select
                value={createRequest.bookType}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    bookType: event.target.value,
                  }))
                }
              >
                <option>Sourcing</option>
                <option>Sales</option>
                <option>Intercompany</option>
              </select>
            </label>
            <label>
              Supply month
              <input
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
              <input
                type="number"
                min="0"
                step="any"
                value={createRequest.volumeRealisedMwh ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    volumeRealisedMwh:
                      event.target.value === "" ? undefined : event.target.value,
                  }))
                }
              />
            </label>
            <div className="toolbar">
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
