import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { z } from 'zod';
import type {
  CreateContractRequest,
  ContractDetailsDto,
  GetContractHistoryResponse,
  UpdateContractRequest,
} from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import { changedFields, shouldAdoptRefreshedDraft } from "../../lib/editor/detailDraftPolicy";
import { clearMutationConflictForEntity } from "../../lib/mutations/mutationCoordinator";
import {
  useCreateContract,
  useDeleteContract,
  useUpdateContract,
  type EntityUpdateVariables,
} from "../../lib/mutations/domainEntityMutations";
import { queryKeys } from '../../lib/query/queryKeys';
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
import { Button } from "../ui/button";
import { EntityCreateDrawer } from "../ui/entity-create-drawer";
import { Frame, FrameDescription, FrameHeader, FramePanel, FrameTitle } from "../ui/frame";
import { Input } from "../ui/input";
import { NumberInput } from "../ui/number-input";
import { RecordDetailPanel } from "../ui/record-detail-panel";
import { RecordActivity } from "../ui/record-activity";
import { TableEditableCell } from "../ui/table-editable-cell";
import { Select } from "../ui/select";
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

function matchesSearch(contract: ContractDetailsDto, term: string): boolean {
  if (term === '') return true;
  const lowered = term.toLowerCase();
  const candidates: Array<unknown> = [
    contract.contractName,
    contract.counterpartyId,
    contract.contractId,
    contract.contractType,
    contract.productType,
    contract.action,
    contract.comment,
    contract.companyShorthand,
    contract.countryCode,
    contract.sourcingCenter,
    contract.salesCenter,
    contract.balancingGroup,
  ];
  return candidates.some((value) => String(value ?? '').toLowerCase().includes(lowered));
}

interface ContractPanelDraft {
  contractName: string;
  counterpartyId: string;
  productType: string;
  action: string;
  companyShorthand: string;
  countryCode: string;
  countryDialCode: string;
  sourcingCenter: string;
  salesCenter: string;
  balancingGroup: string;
  gooQuality: string;
  subsidyStatus: string;
  priceMechanismGas: string;
  fixedPriceGasEurMwh: string;
  contractType: string;
  comment: string;
}

function toDraft(contract: ContractDetailsDto): ContractPanelDraft {
  return {
    contractName: contract.contractName ?? '',
    counterpartyId: contract.counterpartyId ?? '',
    productType: contract.productType ?? '',
    action: contract.action ?? '',
    companyShorthand: contract.companyShorthand ?? '',
    countryCode: contract.countryCode ?? '',
    countryDialCode: contract.countryDialCode == null ? '' : String(contract.countryDialCode),
    sourcingCenter: contract.sourcingCenter ?? '',
    salesCenter: contract.salesCenter ?? '',
    balancingGroup: contract.balancingGroup ?? '',
    gooQuality: contract.gooQuality ?? '',
    subsidyStatus: contract.subsidyStatus ?? '',
    priceMechanismGas: contract.priceMechanismGas ?? '',
    fixedPriceGasEurMwh: contract.fixedPriceGasEurMwh ?? '',
    contractType: contract.contractType ?? '',
    comment: contract.comment ?? '',
  };
}

function draftToChanges(contract: ContractDetailsDto, draft: ContractPanelDraft): ContractChanges {
  return {
    contractName: draft.contractName.trim(),
    counterpartyId: draft.counterpartyId.trim(),
    productType: draft.productType.trim(),
    action: draft.action.trim(),
    companyShorthand: draft.companyShorthand.trim() === '' ? null : draft.companyShorthand.trim(),
    countryCode: draft.countryCode.trim() === '' ? null : draft.countryCode.trim(),
    countryDialCode: draft.countryDialCode.trim() === '' ? null : Number(draft.countryDialCode),
    sourcingCenter: draft.sourcingCenter.trim() === '' ? null : draft.sourcingCenter.trim(),
    salesCenter: draft.salesCenter.trim() === '' ? null : draft.salesCenter.trim(),
    balancingGroup: draft.balancingGroup.trim() === '' ? null : draft.balancingGroup.trim(),
    gooQuality: draft.gooQuality.trim() === '' ? null : draft.gooQuality.trim(),
    subsidyStatus: draft.subsidyStatus.trim() === '' ? null : draft.subsidyStatus.trim(),
    priceMechanismGas: draft.priceMechanismGas.trim() === '' ? null : draft.priceMechanismGas.trim(),
    fixedPriceGasEurMwh: draft.fixedPriceGasEurMwh.trim() === '' ? null : draft.fixedPriceGasEurMwh.trim(),
    contractType: draft.contractType.trim() === '' ? null : draft.contractType.trim(),
    comment: draft.comment.trim() === '' ? null : draft.comment.trim(),
    isActive: contract.isActive,
  };
}

function hasUnsavedChanges(contract: ContractDetailsDto, draft: ContractPanelDraft): boolean {
  return changedFields(toDraft(contract), draft).length > 0;
}

export function ContractsPage() {
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [createRequest, setCreateRequest] =
    useState<CreateContractRequest>(initialCreate);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState<ConflictState>();
  const [search, setSearch] = useState("");
  const [selectedRowIds, setSelectedRowIds] = useState<Set<string>>(new Set());
  const [activeContract, setActiveContract] = useState<ContractDetailsDto>();
  const [panelContractDraft, setPanelContractDraft] = useState<ContractPanelDraft | null>(null);
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
  const contracts = useMemo(() => history.data?.items ?? [], [history.data?.items]);
  const searchableContracts = useMemo(() => {
    const term = search.trim();
    return contracts.filter((contract) => matchesSearch(contract, term.toLowerCase()));
  }, [contracts, search]);
  const selectedCount = selectedRowIds.size;
  const totalCount = history.data?.totalCount ?? 0;

  useEffect(() => {
    if (!activeContract || !panelContractDraft) return;
    const refreshed = contracts.find((contract) => contract.contractId === activeContract.contractId);
    if (!refreshed) return;
    if (!shouldAdoptRefreshedDraft({
      activeVersion: activeContract.version,
      refreshedVersion: refreshed.version,
      dirty: hasUnsavedChanges(activeContract, panelContractDraft),
      refreshedMatchesDraft: !hasUnsavedChanges(refreshed, panelContractDraft),
    })) return;
    setActiveContract(refreshed);
    setPanelContractDraft(toDraft(refreshed));
  }, [activeContract, contracts, panelContractDraft]);

  const save = useCallback(
    async (contract: ContractDetailsDto, changes: ContractChanges) => {
      setError("");
      let version = contract.version;
      const before = currentChanges(contract);
      const intent = changedFields(before, changes);
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
              intent,
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
              intent,
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
              intent: ['isActive'],
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

  const setSelection = useCallback((nextSelectedRowIds: Set<string>) => {
    setSelectedRowIds(nextSelectedRowIds);
  }, []);

  const openContractPanel = useCallback((contract: ContractDetailsDto) => {
    setActiveContract(contract);
    setPanelContractDraft(toDraft(contract));
  }, []);

  const closeContractPanel = useCallback(() => {
    setActiveContract(undefined);
    setPanelContractDraft(null);
  }, []);

  const saveContractField = useCallback(async (
    contract: ContractDetailsDto,
    key: keyof ContractPanelDraft,
    value: string,
  ) => {
    const draft = { ...toDraft(contract), [key]: value };
    await save(contract, draftToChanges(contract, draft));
  }, [save]);

  const columns = useMemo<ColumnDef<ContractDetailsDto>[]>(() => [
      {
        accessorKey: "contractName",
        header: "Contract",
        cell: ({ row }) => <TableEditableCell label="Contract name" value={row.original.contractName} onCommit={(value) => saveContractField(row.original, 'contractName', value)} />,
      },
      { accessorKey: "productType", header: "Product", cell: ({ row }) => <TableEditableCell label="Product" options={productTypes} value={row.original.productType} onCommit={(value) => saveContractField(row.original, 'productType', value)} /> },
      { accessorKey: "action", header: "Action", cell: ({ row }) => <TableEditableCell label="Action" options={actions} value={row.original.action} onCommit={(value) => saveContractField(row.original, 'action', value)} /> },
      { accessorKey: "contractType", header: "Type", cell: ({ row }) => <TableEditableCell label="Contract type" options={contractTypes} value={row.original.contractType ?? 'External'} onCommit={(value) => saveContractField(row.original, 'contractType', value)} /> },
      {
        accessorKey: "isActive",
        header: "Status",
        cell: ({ getValue }) => (
          <TableEditableCell label="Status" readOnly value={getValue() ? "Active" : "Inactive"} />
        ),
      },
  ], [saveContractField]);

  const submitCreate = async (validatedRequest: CreateContractRequest) => {
    setError("");
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

  const submitPanelSave = useCallback(async () => {
    if (!activeContract || !panelContractDraft) return;
    if (!panelContractDraft.contractName.trim()) return;
    await save(activeContract, draftToChanges(activeContract, panelContractDraft));
  }, [activeContract, panelContractDraft, save]);

  const submitPanelDeactivate = useCallback(async () => {
    if (!activeContract) return;
    await deactivate(activeContract);
  }, [activeContract, deactivate]);

  return (
    <section>
      <header className="mb-6 flex items-start justify-between gap-4 max-[800px]:flex-col max-[800px]:items-stretch">
        <div>
          <p className="eyebrow">Master data</p>
          <h2>Contracts</h2>
          <p>
            {history.data
              ? `${history.data.totalCount} records`
              : "Loading contracts…"}
          </p>
        </div>
        <Button type="button" onClick={() => setShowCreate(true)} data-testid="btn-create-contract">
          Create contract
        </Button>
      </header>
      <section className="toolbar" aria-label="Contract list toolbar">
        <label>
          Search contracts
          <Input
            aria-label="Search contracts"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            type="search"
            placeholder="Search contract name, counterparty, type..."
          />
        </label>
        <div aria-label="Selection">
          {selectedCount} selected
        </div>
        <div aria-label="Record count">
          {searchableContracts.length} of {totalCount} contracts
        </div>
      </section>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {history.isError && <p role="alert">Unable to load contracts.</p>}
      {!history.isError && (
        <VirtualizedDataTable
          testId="virtual-contracts-grid"
          data={searchableContracts}
          columns={columns}
          ariaLabel="Contracts"
          getRowId={(row) => row.contractId}
          onRowOpen={openContractPanel}
          selectedRowIds={selectedRowIds}
          onSelectedRowIdsChange={setSelection}
        />
      )}
      {history.data && (
        <nav className="flex flex-wrap items-center gap-2" aria-label="Contract history pages">
          <button type="button" disabled={page === 1 || history.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button>
          <span>Page {page}</span>
          <button type="button" disabled={!history.data.hasNextPage || history.isFetching} onClick={() => setPage((value) => value + 1)}>Next</button>
        </nav>
      )}
      <EntityCreateDrawer
        description="Create a contract and define its commercial classification."
        onOpenChange={setShowCreate}
        open={showCreate}
        title="Create contract"
      >
          <ValidatedForm schema={createContractSchema} values={createRequest} onValid={submitCreate}>
            <label>
              Contract name
              <Input
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
              <Input
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
            <div data-slot="entity-create-field">
              <span>Product type</span>
              <Select
                label="Product type"
                options={productTypes}
                value={createRequest.productType}
                onValueChange={(productType) => setCreateRequest((value) => ({ ...value, productType }))}
              />
            </div>
            <div data-slot="entity-create-field">
              <span>Action</span>
              <Select
                label="Action"
                options={actions}
                value={createRequest.action}
                onValueChange={(action) => setCreateRequest((value) => ({ ...value, action }))}
              />
            </div>
            <div data-slot="entity-create-field">
              <span>Contract type</span>
              <Select
                label="Contract type"
                options={contractTypes}
                value={createRequest.contractType ?? 'External'}
                onValueChange={(contractType) => setCreateRequest((value) => ({ ...value, contractType }))}
              />
            </div>
            <label>
              Comment
              <Input
                value={createRequest.comment ?? ""}
                onChange={(event) =>
                  setCreateRequest((value) => ({
                    ...value,
                    comment: event.target.value || undefined,
                  }))
                }
              />
            </label>
            <div data-slot="entity-create-drawer-actions" className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="secondary"
                onClick={() => setShowCreate(false)}
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
            onClose={() => {
              clearMutationConflictForEntity(conflict.id);
              setConflict(undefined);
            }}
          />
        </div>
      )}
      {activeContract && panelContractDraft && (
        <RecordDetailPanel
          open={Boolean(activeContract)}
          onOpenChange={(open) => {
            if (!open) {
              closeContractPanel();
            }
          }}
          eyebrow="Contract"
          title={activeContract.contractName}
          description="Edit contract details and save the updated values."
          recordId={activeContract.contractId}
          version={activeContract.version}
          dirty={hasUnsavedChanges(activeContract, panelContractDraft)}
          properties={(
            <div data-slot="record-field-grid">
              <div data-slot="record-field">
                <span>Contract name</span>
                <Input
                  aria-label="Contract name"
                  value={panelContractDraft.contractName}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, contractName: event.target.value } : draft,
                    )
                  }
                />
              </div>
              <div data-slot="record-field">
                <span>Counterparty ID</span>
                <Input
                  aria-label="Counterparty ID"
                  value={panelContractDraft.counterpartyId}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, counterpartyId: event.target.value } : draft,
                    )
                  }
                />
              </div>
              <div data-slot="record-field">
                <span>Product</span>
                <Select
                  label="Product type"
                  options={productTypes}
                  value={panelContractDraft.productType}
                  onValueChange={(value) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, productType: value } : draft,
                    )
                  }
                />
              </div>
              <div data-slot="record-field">
                <span>Action</span>
                <Select
                  label="Action"
                  options={actions}
                  value={panelContractDraft.action}
                  onValueChange={(value) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, action: value } : draft,
                    )
                  }
                />
              </div>
              <div data-slot="record-field">
                <span>Contract type</span>
                <Select
                  label="Contract type"
                  options={contractTypes}
                  value={panelContractDraft.contractType}
                  onValueChange={(value) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, contractType: value } : draft,
                    )
                  }
                />
              </div>
              <label data-slot="record-field">
                <span>Company shorthand</span>
                <Input
                  aria-label="Company shorthand"
                  value={panelContractDraft.companyShorthand}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, companyShorthand: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Country code</span>
                <Input
                  aria-label="Country code"
                  value={panelContractDraft.countryCode}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, countryCode: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Country dial code</span>
                <NumberInput
                  aria-label="Country dial code"
                  value={panelContractDraft.countryDialCode}
                  onValueChange={(value) =>
                    setPanelContractDraft((draft) =>
                      draft
                        ? { ...draft, countryDialCode: value }
                        : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Sourcing center</span>
                <Input
                  aria-label="Sourcing center"
                  value={panelContractDraft.sourcingCenter}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, sourcingCenter: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Sales center</span>
                <Input
                  aria-label="Sales center"
                  value={panelContractDraft.salesCenter}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, salesCenter: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Balancing group</span>
                <Input
                  aria-label="Balancing group"
                  value={panelContractDraft.balancingGroup}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, balancingGroup: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>GoO quality</span>
                <Input
                  aria-label="GoO quality"
                  value={panelContractDraft.gooQuality}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, gooQuality: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Subsidy status</span>
                <Input
                  aria-label="Subsidy status"
                  value={panelContractDraft.subsidyStatus}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, subsidyStatus: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Price mechanism gas</span>
                <Input
                  aria-label="Price mechanism gas"
                  value={panelContractDraft.priceMechanismGas}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, priceMechanismGas: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Fixed price Gas EUR/MWh</span>
                <Input
                  aria-label="Fixed price Gas EUR/MWh"
                  value={panelContractDraft.fixedPriceGasEurMwh}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, fixedPriceGasEurMwh: event.target.value } : draft,
                    )
                  }
                />
              </label>
              <label data-slot="record-field">
                <span>Comment</span>
                <textarea
                  aria-label="Comment"
                  value={panelContractDraft.comment}
                  onChange={(event) =>
                    setPanelContractDraft((draft) =>
                      draft ? { ...draft, comment: event.target.value } : draft,
                    )
                  }
                />
              </label>
            </div>
          )}
          context={(
            <div data-slot="record-field-grid">
              <Frame>
                <FrameHeader>
                  <FrameTitle>Read-only details</FrameTitle>
                  <FrameDescription>Values tracked by Tradebook.</FrameDescription>
                </FrameHeader>
                <FramePanel>
                  <dl data-slot="record-facts">
                    <div>
                      <dt>Status</dt>
                      <dd>{activeContract.isActive ? "Active" : "Inactive"}</dd>
                    </div>
                    <div>
                      <dt>Created</dt>
                      <dd>{new Date(activeContract.createdAt).toLocaleString()}</dd>
                    </div>
                    <div>
                      <dt>Updated</dt>
                      <dd>{new Date(activeContract.updatedAt).toLocaleString()}</dd>
                    </div>
                  </dl>
                </FramePanel>
              </Frame>
            </div>
          )}
          activity={<RecordActivity entityId={activeContract.contractId} entityName="contracts" />}
          actions={(
            <>
              <Button
                type="button"
                disabled={updateMutation.isPending || !hasUnsavedChanges(activeContract, panelContractDraft) || !panelContractDraft.contractName.trim()}
                onClick={() => void submitPanelSave()}
              >
                Save
              </Button>
              <Button
                intent="danger"
                type="button"
                disabled={deleteMutation.isPending || !activeContract.isActive}
                onClick={() => void submitPanelDeactivate()}
              >
                Deactivate
              </Button>
            </>
          )}
        />
      )}
    </section>
  );
}
