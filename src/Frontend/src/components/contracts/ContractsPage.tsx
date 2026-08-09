import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useRef, useState } from "react";
import { z } from 'zod';
import type { ContractDetailsDto } from "../../api/generated/types.gen";
import type { CreateContractRequest } from "../../api/generated/types.gen";
import type { GetContractHistoryResponse } from "../../api/generated/types.gen";
import type { UpdateContractRequest } from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import {
  useCreateContract,
  useDeleteContract,
  useUpdateContract,
  type EntityUpdateVariables,
} from "../../lib/mutations/domainEntityMutations";
import { queryKeys } from '../../lib/query/queryKeys';
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
import { ValidatedForm } from '../ui/validated-form';

const productTypes = [
  "GoO",
  "Gas",
  "GoO+Gas",
  "GoO+Gas+Shipping",
  "Tickets",
] as const;
const actions = ["Buy", "Sell", "Intercompany", "Swap"] as const;
const contractTypes = ["External", "Intercompany"] as const;
type ContractChanges = Omit<UpdateContractRequest, "contractId" | "version">;
interface ConflictState {
  id: string;
  serverState?: ContractDetailsDto;
  attempted: object;
}

const initialCreate: CreateContractRequest = {
  contractName: "",
  counterpartyId: "",
  productType: "Gas",
  action: "Buy",
  contractType: "External",
};
const createContractSchema = z.custom<CreateContractRequest>((candidate): candidate is CreateContractRequest => {
  const value = candidate as Partial<CreateContractRequest>;
  return typeof value.contractName === 'string' && value.contractName.trim().length > 0 && value.contractName.length <= 100
    && typeof value.counterpartyId === 'string' && value.counterpartyId.trim().length > 0
    && typeof value.productType === 'string' && productTypes.includes(value.productType as (typeof productTypes)[number])
    && typeof value.action === 'string' && actions.includes(value.action as (typeof actions)[number]);
}, { error: 'Complete the required contract fields.' });

function currentChanges(
  contract: ContractDetailsDto,
  contractName = contract.contractName,
): ContractChanges {
  return {
    contractName,
    counterpartyId: contract.counterpartyId,
    productType: contract.productType,
    action: contract.action,
    companyShorthand: contract.companyShorthand,
    countryCode: contract.countryCode,
    countryDialCode: contract.countryDialCode,
    sourcingCenter: contract.sourcingCenter,
    salesCenter: contract.salesCenter,
    balancingGroup: contract.balancingGroup,
    gooQuality: contract.gooQuality,
    subsidyStatus: contract.subsidyStatus,
    priceMechanismGas: contract.priceMechanismGas,
    fixedPriceGasEurMwh: contract.fixedPriceGasEurMwh,
    contractType: contract.contractType,
    comment: contract.comment,
    isActive: contract.isActive,
  };
}

function ContractEditor({
  contract,
  onSave,
  onDeactivate,
}: {
  contract: ContractDetailsDto;
  onSave: (contract: ContractDetailsDto, changes: ContractChanges) => void;
  onDeactivate: (contract: ContractDetailsDto) => void;
}) {
  const [name, setName] = useState(contract.contractName);
  return (
    <div className="grid grid-cols-4 items-center gap-2 max-[800px]:grid-cols-2">
      <input
        aria-label={`Contract name for ${contract.contractId}`}
        value={name}
        onChange={(event) => setName(event.target.value)}
      />
      <button
        type="button"
        disabled={!name.trim() || name === contract.contractName}
        onClick={() => onSave(contract, currentChanges(contract, name.trim()))}
      >
        Save
      </button>
      <button
        type="button"
        className="bg-red-700"
        disabled={!contract.isActive}
        onClick={() => onDeactivate(contract)}
      >
        Deactivate
      </button>
    </div>
  );
}

export function ContractsPage() {
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [createRequest, setCreateRequest] =
    useState<CreateContractRequest>(initialCreate);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState<ConflictState>();
  const attempted = useRef<object>({});
  const commandStack = useCommandStack();
  const onConflict = useCallback(
    (id: string, serverState?: ContractDetailsDto) =>
      setConflict({ id, serverState, attempted: attempted.current }),
    [],
  );
  const onError = useCallback(
    (failure: unknown) =>
      setError(
        failure instanceof Error
          ? failure.message
          : "The contract change could not be saved.",
      ),
    [],
  );
  const createMutation = useCreateContract(onConflict, onError);
  const updateMutation = useUpdateContract(onConflict, onError);
  const deleteMutation = useDeleteContract(onConflict, onError);
  const history = useQuery({
    queryKey: queryKeys.contracts.list({ page, pageSize: 100 }),
    queryFn: ({ signal }) =>
      apiFetch<GetContractHistoryResponse>(`/api/v1/contracts?page=${page}&pageSize=100`, { signal }),
  });

  const save = useCallback(
    async (contract: ContractDetailsDto, changes: ContractChanges) => {
      setError("");
      attempted.current = changes;
      let version = contract.version;
      const before = currentChanges(contract);
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Update ${contract.contractName}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = changes;
          version = (
            await updateMutation.mutateAsync({
              id: contract.contractId,
              version,
              changes,
            } satisfies EntityUpdateVariables<ContractChanges>)
          ).version;
        },
        undo: async () => {
          attempted.current = before;
          version = (
            await updateMutation.mutateAsync({
              id: contract.contractId,
              version,
              changes: before,
            })
          ).version;
        },
      };
      try {
        await commandStack.execute(command);
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, updateMutation],
  );

  const deactivate = useCallback(
    async (contract: ContractDetailsDto) => {
      const reason = "Deactivated from Tradebook UI";
      attempted.current = { reason };
      let version = contract.version;
      const restore = { ...currentChanges(contract), isActive: true };
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Deactivate ${contract.contractName}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = { reason };
          await deleteMutation.mutateAsync({
            id: contract.contractId,
            version,
            reason,
          });
          version = (
            await apiFetch<ContractDetailsDto>(
              `/api/v1/contracts/${contract.contractId}`,
            )
          ).version;
        },
        undo: async () => {
          attempted.current = restore;
          version = (
            await updateMutation.mutateAsync({
              id: contract.contractId,
              version,
              changes: restore,
            })
          ).version;
        },
      };
      try {
        await commandStack.execute(command);
      } catch {
        /* mutation callbacks surface the error */
      }
    },
    [commandStack, deleteMutation, updateMutation],
  );

  const columns = useMemo<ColumnDef<ContractDetailsDto>[]>(
    () => [
      { accessorKey: "contractName", header: "Contract" },
      { accessorKey: "productType", header: "Product" },
      { accessorKey: "action", header: "Action" },
      { accessorKey: "contractType", header: "Type" },
      {
        accessorKey: "isActive",
        header: "Active",
        cell: ({ getValue }) => (getValue() ? "Yes" : "No"),
      },
      {
        id: "edit",
        header: "Edit",
        cell: ({ row }) => (
          <ContractEditor
            key={row.original.version}
            contract={row.original}
            onSave={(contract, changes) => void save(contract, changes)}
            onDeactivate={(contract) => void deactivate(contract)}
          />
        ),
      },
    ],
    [deactivate, save],
  );

  const submitCreate = async (validatedRequest: CreateContractRequest) => {
    setError("");
    attempted.current = validatedRequest;
    try {
      const request = { ...validatedRequest };
      let created: ContractDetailsDto | undefined;
      let version = 0;
      const command: Command = {
        id: crypto.randomUUID(),
        description: `Create ${request.contractName}`,
        timestamp: Date.now(),
        execute: async () => {
          attempted.current = request;
          if (!created) {
            created = await createMutation.mutateAsync(request);
            version = created.version;
            return;
          }
          const restored = await updateMutation.mutateAsync({
            id: created.contractId,
            version,
            changes: { ...currentChanges(created), isActive: true },
          });
          created = restored;
          version = restored.version;
        },
        undo: async () => {
          await deleteMutation.mutateAsync({
            id: created!.contractId,
            version,
            reason: "Undo create",
          });
          version = (
            await apiFetch<ContractDetailsDto>(
              `/api/v1/contracts/${created!.contractId}`,
            )
          ).version;
        },
      };
      await commandStack.execute(command);
      setCreateRequest(initialCreate);
      setShowCreate(false);
    } catch {
      /* mutation callbacks surface the error */
    }
  };

  return (
    <section>
      <header className="mb-6 flex items-start justify-between gap-4 max-[800px]:flex-col max-[800px]:items-stretch">
        <div>
          <p className="mb-1 text-xs font-extrabold uppercase tracking-widest text-gray-600">Master data</p>
          <h2>Contracts</h2>
          <p>
            {history.data
              ? `${history.data.totalCount} records`
              : "Loading contract history…"}
          </p>
        </div>
        <button
          data-testid="btn-create-contract"
          type="button"
          onClick={() => setShowCreate(true)}
        >
          Create contract
        </button>
      </header>
      {error && (
        <p role="alert" className="rounded-lg bg-red-100 p-3 text-red-900">
          {error}
        </p>
      )}
      {history.isError && <p role="alert">Unable to load contracts.</p>}
      {!history.isError && (
        <VirtualizedDataTable
          testId="virtual-contracts-grid"
          data={history.data?.items ?? []}
          columns={columns}
          getRowId={(row) => row.contractId}
        />
      )}
      {history.data && (
        <nav className="flex flex-wrap items-center gap-2" aria-label="Contract history pages">
          <button type="button" disabled={page === 1 || history.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button>
          <span>Page {page}</span>
          <button type="button" disabled={!history.data.hasNextPage || history.isFetching} onClick={() => setPage((value) => value + 1)}>Next</button>
        </nav>
      )}
      {showCreate && (
        <section
          className="fixed inset-0 z-20 flex items-center justify-center bg-black/50 p-4"
          role="dialog"
          aria-modal="true"
          aria-label="Create contract"
        >
          <ValidatedForm schema={createContractSchema} values={createRequest} onValid={submitCreate}>
            <h3>Create contract</h3>
            <label>
              Contract name
              <input
                required
                maxLength={100}
                value={createRequest.contractName}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    contractName: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              Counterparty ID
              <input
                required
                value={createRequest.counterpartyId}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    counterpartyId: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              Product type
              <select
                value={createRequest.productType}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    productType: event.target.value,
                  }))
                }
              >
                {productTypes.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </select>
            </label>
            <label>
              Action
              <select
                value={createRequest.action}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    action: event.target.value,
                  }))
                }
              >
                {actions.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </select>
            </label>
            <label>
              Contract type
              <select
                value={createRequest.contractType ?? "External"}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    contractType: event.target.value,
                  }))
                }
              >
                {contractTypes.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </select>
            </label>
            <label>
              Comment
              <input
                value={createRequest.comment ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    comment: event.target.value || undefined,
                  }))
                }
              />
            </label>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="bg-gray-200 text-gray-800"
                onClick={() => setShowCreate(false)}
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
        <div className="fixed inset-0 z-20 flex items-center justify-center bg-black/50 p-4">
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
