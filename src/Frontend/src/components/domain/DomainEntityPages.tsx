import { useQuery } from "@tanstack/react-query";
import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { z } from "zod";
import type { BioticketDetailsDto } from "../../api/generated/types.gen";
import type { CapacityBookingDetailsDto } from "../../api/generated/types.gen";
import type { CreateBioticketRequest } from "../../api/generated/types.gen";
import type { CreateCapacityBookingRequest } from "../../api/generated/types.gen";
import type { CreateGooCertificateTransactionRequest } from "../../api/generated/types.gen";
import type { CreateHedgeRequest } from "../../api/generated/types.gen";
import type { CreateTaxTariffRequest } from "../../api/generated/types.gen";
import type { CreateTransferRequest } from "../../api/generated/types.gen";
import type { GooCertificateTransactionDetailsDto } from "../../api/generated/types.gen";
import type { HedgeDetailsDto } from "../../api/generated/types.gen";
import type { TaxTariffDetailsDto } from "../../api/generated/types.gen";
import type { TransferDetailsDto } from "../../api/generated/types.gen";
import type { UpdateBioticketRequest } from "../../api/generated/types.gen";
import type { UpdateCapacityBookingRequest } from "../../api/generated/types.gen";
import type { UpdateGooCertificateTransactionRequest } from "../../api/generated/types.gen";
import type { UpdateHedgeRequest } from "../../api/generated/types.gen";
import type { UpdateTaxTariffRequest } from "../../api/generated/types.gen";
import type { UpdateTransferRequest } from "../../api/generated/types.gen";
import { apiFetch } from "../../lib/api/client";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import type { Command } from "../../lib/commands/UndoRedoStack";
import { useContractOptions } from "../../lib/query/useContractOptions";
import {
    domainQueryKeys,
    type EntityDeleteVariables,
    type EntityUpdateVariables,
    type PagedEntityCache,
    useCreateBioticket,
    useCreateCapacityBooking,
    useCreateGooCertificate,
    useCreateHedge,
    useCreateTaxTariff,
    useCreateTransfer,
    useDeleteBioticket,
    useDeleteCapacityBooking,
    useDeleteGooCertificate,
    useDeleteHedge,
    useDeleteTaxTariff,
    useDeleteTransfer,
    useRequestGooBatchExport,
    useUpdateBioticket,
    useUpdateCapacityBooking,
    useUpdateGooCertificate,
    useUpdateHedge,
    useUpdateTaxTariff,
    useUpdateTransfer,
} from "../../lib/mutations/domainEntityMutations";
import { listQueryKey } from "../../lib/query/queryKeys";
import {
    isMoneyString,
    moneyInputField,
    normalizeMoneyInput,
} from "../../lib/validation/money-input";
import { VirtualizedDataTable } from "../grid/VirtualizedDataTable";
import { ConflictDialog } from "../ui/ConflictDialog";
import { Button } from "../ui/button";
import { Combobox, type ComboboxOption } from "../ui/combobox";
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
import { Select } from "../ui/select";
import { RecordDetailPanel } from "../ui/record-detail-panel";
import { RecordActivity } from "../ui/record-activity";
import { TableEditableCell } from "../ui/table-editable-cell";
import { ValidatedForm } from "../ui/validated-form";

type Versioned = { version: number };
type InputKind = "text" | "number" | "date";
interface FieldSpec {
    key: string;
    label: string;
    kind?: InputKind;
    required?: boolean;
    options?: readonly string[];
}
interface MutationLike<TResult, TVariables> {
    mutateAsync: (variables: TVariables) => Promise<TResult>;
    isPending: boolean;
}
interface Feedback<T> {
    attempted: { current: object };
    error: string;
    setError: (message: string) => void;
    conflict?: { id: string; serverState?: T; attempted: object };
    setConflict: (
        value: { id: string; serverState?: T; attempted: object } | undefined,
    ) => void;
    onConflict: (id: string, serverState?: T) => void;
    onError: (failure: unknown) => void;
}
const reportStatuses = [
    "Completed - Payment Received/Sent",
    "In Progress - Invoice Received/Sent",
    "Pending - No Invoice",
    "Cancelled",
    "Awaiting",
    "Issue",
] as const;
const certificateStatuses = [
    "Latest transaction",
    "Batch export requested",
    "Processing",
    "Completed",
    "Failed",
] as const;
const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => `${new Date().toISOString().slice(0, 7)}-01`;

function asRecord(value: object): Record<string, unknown> {
    return value as Record<string, unknown>;
}
function fieldValue(value: object, key: string): string {
    const raw = asRecord(value)[key];
    return raw === null || raw === undefined ? "" : String(raw);
}
// 'number' fields are Money strings on the wire; keep raw string input state for validation at save time.
function changedValue(raw: string, field: FieldSpec): unknown {
    return field.kind === "number"
        ? raw === ""
            ? null
            : raw
        : raw === "" && !field.required
          ? null
          : raw;
}
function changeField<T extends object>(
    value: T,
    field: FieldSpec,
    raw: string,
): T {
    return { ...value, [field.key]: changedValue(raw, field) };
}
function displayValue(value: unknown): string {
    return value === null || value === undefined || value === ""
        ? "—"
        : String(value);
}

function activityEntityName(basePath: string): string {
    const resource = basePath.split("/").at(-1);
    if (resource === "biotickets") return "bioticket_deliveries";
    if (resource === "goo-certificates") return "goo_certificate_transactions";
    return (resource ?? "").replaceAll("-", "_");
}

function FieldInput<T extends object>({
    field,
    value,
    onChange,
    contractOptions,
}: {
    field: FieldSpec;
    value: T;
    onChange: (next: T) => void;
    contractOptions?: readonly ComboboxOption[];
}) {
    const current = fieldValue(value, field.key);
    if (field.key === "contractId" && contractOptions) {
        return (
            <Combobox
                label={field.label}
                value={current}
                options={contractOptions}
                onChange={(next) => onChange(changeField(value, field, next))}
            />
        );
    }
    if (field.options) {
        const selectOptions = field.required
            ? field.options
            : ["Not set", ...field.options];
        return (
            <>
                <label>{field.label}</label>
                <Select
                    label={field.label}
                    options={selectOptions}
                    value={current === "" ? "Not set" : current}
                    onValueChange={(next) =>
                        onChange(
                            changeField(
                                value,
                                field,
                                next === "Not set" ? "" : next,
                            ),
                        )
                    }
                />
            </>
        );
    }
    if (field.kind === "number") {
        return (
            <label>
                {field.label}
                <NumberInput
                    aria-label={field.label}
                    required={field.required}
                    value={current}
                    onValueChange={(nextValue) =>
                        onChange(changeField(value, field, nextValue))
                    }
                />
            </label>
        );
    }
    return (
        <label>
            {field.label}
            <Input
                required={field.required}
                type={field.kind ?? "text"}
                value={current}
                onChange={(event) =>
                    onChange(changeField(value, field, event.target.value))
                }
            />
        </label>
    );
}
function useFeedback<T>(fallback: string): Feedback<T> {
    const attempted = useRef<object>({});
    const [error, setError] = useState("");
    const [conflict, setConflict] = useState<Feedback<T>["conflict"]>();
    const onConflict = useCallback(
        (id: string, serverState?: T) =>
            setConflict({ id, serverState, attempted: attempted.current }),
        [],
    );
    const onError = useCallback(
        (failure: unknown) =>
            setError(failure instanceof Error ? failure.message : fallback),
        [fallback],
    );
    return {
        attempted,
        error,
        setError,
        conflict,
        setConflict,
        onConflict,
        onError,
    };
}

interface CrudPageProps<
    T extends Versioned,
    TCreate extends object,
    TChanges extends object,
> {
    title: string;
    eyebrow: string;
    basePath: string;
    queryKey: readonly string[];
    idOf: (entity: T) => string;
    labelOf: (entity: T) => string;
    initialCreate: () => TCreate;
    createFromEntity: (entity: T) => TCreate;
    changesFromEntity: (entity: T) => TChanges;
    createFields: FieldSpec[];
    editFields: FieldSpec[];
    displayFields: FieldSpec[];
    createMutation: MutationLike<T, TCreate>;
    updateMutation: MutationLike<T, EntityUpdateVariables<TChanges>>;
    deleteMutation: MutationLike<void, EntityDeleteVariables>;
    feedback: Feedback<T>;
    validateCreate?: (request: TCreate) => string | undefined;
    cancelInsteadOfDelete?: boolean;
    extraAction?: {
        label: string;
        run: (entity: T) => Promise<T>;
        disabled?: (entity: T) => boolean;
    };
}

function matchesSearch<T extends Versioned>(
    entity: T,
    term: string,
    props: Pick<
        CrudPageProps<T, object, object>,
        "idOf" | "labelOf" | "displayFields" | "editFields"
    >,
): boolean {
    const search = term.trim();
    if (!search) return true;
    const lowered = search.toLowerCase();
    const candidates: unknown[] = [props.labelOf(entity), props.idOf(entity)];
    for (const field of [...props.displayFields, ...props.editFields]) {
        candidates.push(asRecord(entity)[field.key]);
    }
    return candidates.some((value) =>
        String(value ?? "")
            .toLowerCase()
            .includes(lowered),
    );
}

function DomainCrudPage<
    T extends Versioned,
    TCreate extends object,
    TChanges extends object,
>(props: CrudPageProps<T, TCreate, TChanges>) {
    const [page, setPage] = useState(1);
    const [showCreate, setShowCreate] = useState(false);
    const [createRequest, setCreateRequest] = useState<TCreate>(
        props.initialCreate,
    );
    const [search, setSearch] = useState("");
    const [selectedRowIds, setSelectedRowIds] = useState<Set<string>>(
        new Set(),
    );
    const [activeEntity, setActiveEntity] = useState<T>();
    const [activeChanges, setActiveChanges] = useState<TChanges>();
    const { options: contractOptions } = useContractOptions();
    const commandStack = useCommandStack();
    const createSchema = useMemo(() => {
        const shape: Record<string, z.ZodType<unknown>> = {};
        for (const field of props.createFields) {
            shape[field.key] =
                field.kind === "number"
                    ? moneyInputField({
                          label: field.label,
                          required: field.required,
                      })
                    : field.required
                      ? z
                            .string({ error: `${field.label} is required.` })
                            .min(1, { error: `${field.label} is required.` })
                      : z.string().nullish();
        }
        return z.object(shape) as unknown as z.ZodType<TCreate>;
    }, [props.createFields]);
    const history = useQuery({
        queryKey: listQueryKey(props.queryKey, { page, pageSize: 100 }),
        queryFn: ({ signal }) =>
            apiFetch<PagedEntityCache<T>>(
                `${props.basePath}?page=${page}&pageSize=100`,
                { signal },
            ),
    });
    const entities = useMemo(
        () => history.data?.items ?? [],
        [history.data?.items],
    );
    const searchableEntities = useMemo(
        () => entities.filter((entity) => matchesSearch(entity, search, props)),
        [entities, search, props],
    );
    const activeId = activeEntity ? props.idOf(activeEntity) : undefined;
    const selectedCount = selectedRowIds.size;

    useEffect(() => {
        if (!activeEntity || !activeId) return;
        const refreshed = entities.find(
            (entity) => props.idOf(entity) === activeId,
        );
        if (!refreshed) {
            setActiveEntity(undefined);
            setActiveChanges(undefined);
            return;
        }
        setActiveEntity(refreshed);
        setActiveChanges(props.changesFromEntity(refreshed));
    }, [activeEntity, activeId, entities, props]);

    const openEntityPanel = useCallback(
        (entity: T) => {
            setActiveEntity(entity);
            setActiveChanges(props.changesFromEntity(entity));
        },
        [props],
    );

    const closeEntityPanel = useCallback(() => {
        setActiveEntity(undefined);
        setActiveChanges(undefined);
    }, []);

    const save = useCallback(
        async (entity: T, requested: TChanges) => {
            props.feedback.setError("");
            const changes = Object.fromEntries(
                props.editFields.map((field) => {
                    const raw = asRecord(requested)[field.key];
                    return [
                        field.key,
                        field.kind === "number" && typeof raw === "string"
                            ? normalizeMoneyInput(raw)
                            : raw,
                    ];
                }),
            ) as TChanges;
            const invalidField = props.editFields.find((field) => {
                const value = asRecord(changes)[field.key];
                return (
                    field.kind === "number" &&
                    typeof value === "string" &&
                    !isMoneyString(value)
                );
            });
            if (invalidField) {
                props.feedback.setError(
                    `${invalidField.label} must be a decimal number (for example 12.5).`,
                );
                return;
            }
            const before = props.changesFromEntity(entity);
            let current = entity;
            const command: Command = {
                id: crypto.randomUUID(),
                description: `Update ${props.labelOf(entity)}`,
                timestamp: Date.now(),
                execute: async () => {
                    props.feedback.attempted.current = changes;
                    current = await props.updateMutation.mutateAsync({
                        id: props.idOf(current),
                        version: current.version,
                        changes,
                    });
                },
                undo: async () => {
                    props.feedback.attempted.current = before;
                    current = await props.updateMutation.mutateAsync({
                        id: props.idOf(current),
                        version: current.version,
                        changes: before,
                    });
                },
            };
            try {
                await commandStack.execute(command);
            } catch {
                /* mutation callbacks own user-visible failures */
            }
        },
        [commandStack, props],
    );

    const remove = useCallback(
        async (entity: T) => {
            props.feedback.setError("");
            const reason = props.cancelInsteadOfDelete
                ? "Cancelled from Tradebook UI"
                : "Deleted from Tradebook UI";
            const restoreChanges = props.changesFromEntity(entity);
            let current = entity;
            const command: Command = {
                id: crypto.randomUUID(),
                description: `${props.cancelInsteadOfDelete ? "Cancel" : "Delete"} ${props.labelOf(entity)}`,
                timestamp: Date.now(),
                execute: async () => {
                    props.feedback.attempted.current = { reason };
                    await props.deleteMutation.mutateAsync({
                        id: props.idOf(current),
                        version: current.version,
                        reason,
                    });
                    if (props.cancelInsteadOfDelete)
                        current = await apiFetch<T>(
                            `${props.basePath}/${encodeURIComponent(props.idOf(current))}`,
                        );
                },
                undo: async () => {
                    props.feedback.attempted.current = restoreChanges;
                    current = props.cancelInsteadOfDelete
                        ? await props.updateMutation.mutateAsync({
                              id: props.idOf(current),
                              version: current.version,
                              changes: restoreChanges,
                          })
                        : await props.createMutation.mutateAsync(
                              props.createFromEntity(entity),
                          );
                },
            };
            try {
                await commandStack.execute(command);
            } catch {
                /* mutation callbacks own user-visible failures */
            }
            closeEntityPanel();
        },
        [closeEntityPanel, commandStack, props],
    );

    const runExtraAction = useCallback(
        async (entity: T) => {
            if (!props.extraAction) return;
            const before = props.changesFromEntity(entity);
            let current = entity;
            const command: Command = {
                id: crypto.randomUUID(),
                description: `${props.extraAction.label}: ${props.labelOf(entity)}`,
                timestamp: Date.now(),
                execute: async () => {
                    props.feedback.attempted.current = {
                        action: props.extraAction!.label,
                    };
                    current = await props.extraAction!.run(current);
                },
                undo: async () => {
                    props.feedback.attempted.current = before;
                    current = await props.updateMutation.mutateAsync({
                        id: props.idOf(current),
                        version: current.version,
                        changes: before,
                    });
                },
            };
            try {
                await commandStack.execute(command);
            } catch {
                /* mutation callbacks own user-visible failures */
            }
            closeEntityPanel();
        },
        [closeEntityPanel, commandStack, props],
    );

    const onSelectedRowIdsChange = useCallback((next: Set<string>) => {
        setSelectedRowIds(next);
    }, []);

    const submitCreate = async (validatedRequest: TCreate) => {
        props.feedback.setError("");
        const request = { ...validatedRequest };
        const validationError = props.validateCreate?.(request);
        if (validationError) {
            props.feedback.setError(validationError);
            return;
        }
        let current: T | undefined;
        let restoreChanges: TChanges | undefined;
        const command: Command = {
            id: crypto.randomUUID(),
            description: `Create ${props.title}`,
            timestamp: Date.now(),
            execute: async () => {
                props.feedback.attempted.current = request;
                if (props.cancelInsteadOfDelete && current && restoreChanges) {
                    current = await props.updateMutation.mutateAsync({
                        id: props.idOf(current),
                        version: current.version,
                        changes: restoreChanges,
                    });
                } else {
                    current = await props.createMutation.mutateAsync(request);
                    restoreChanges = props.changesFromEntity(current);
                }
            },
            undo: async () => {
                if (!current) return;
                await props.deleteMutation.mutateAsync({
                    id: props.idOf(current),
                    version: current.version,
                    reason: "Undo create",
                });
                if (props.cancelInsteadOfDelete)
                    current = await apiFetch<T>(
                        `${props.basePath}/${encodeURIComponent(props.idOf(current))}`,
                    );
            },
        };
        try {
            await commandStack.execute(command);
            setCreateRequest(props.initialCreate());
            setShowCreate(false);
        } catch {
            /* mutation callbacks own user-visible failures */
        }
    };

    const [labelField, ...detailFields] = props.displayFields;
    const labelFieldKey = labelField?.key;
    const commitInlineField = useCallback(
        async (entity: T, field: FieldSpec, raw: string) => {
            const requested = changeField(
                props.changesFromEntity(entity),
                field,
                raw,
            );
            await save(entity, requested);
        },
        [props, save],
    );
    const columns = useMemo<ColumnDef<T>[]>(() => {
        const editableLabelField = labelField
            ? props.editFields.find((field) => field.key === labelField.key)
            : undefined;
        const primaryColumn: ColumnDef<T> = {
            id: labelFieldKey ?? "label",
            header: labelField?.label ?? "Record",
            accessorFn: labelField?.key
                ? (row) => asRecord(row)[labelField.key]
                : (row) => props.labelOf(row),
            cell: ({ row }) => editableLabelField ? (
                <TableEditableCell
                    kind={editableLabelField.kind}
                    label={editableLabelField.label}
                    onCommit={(raw) => commitInlineField(row.original, editableLabelField, raw)}
                    options={editableLabelField.options}
                    value={fieldValue(row.original, editableLabelField.key)}
                />
            ) : (
                <Button intent="ghost" type="button" aria-label={`Open ${props.title} ${props.labelOf(row.original)}`} onClick={() => openEntityPanel(row.original)}>
                    {props.labelOf(row.original)}
                </Button>
            ),
        };
        const dataColumns = detailFields.map((field): ColumnDef<T> => {
            const editableField = props.editFields.find((candidate) => candidate.key === field.key);
            return {
                id: field.key,
                header: field.label,
                accessorFn: (row) => asRecord(row)[field.key],
                cell: ({ getValue, row }) => editableField ? (
                    <TableEditableCell
                        kind={editableField.kind}
                        label={editableField.label}
                        onCommit={(raw) => commitInlineField(row.original, editableField, raw)}
                        options={editableField.options}
                        value={fieldValue(row.original, editableField.key)}
                    />
                ) : (
                    <TableEditableCell label={field.label} readOnly value={displayValue(getValue())} />
                ),
            };
        });
        return [primaryColumn, ...dataColumns];
    }, [
        detailFields,
        labelField,
        labelFieldKey,
        commitInlineField,
        openEntityPanel,
        props,
    ]);

    const submitPanelSave = useCallback(async () => {
        if (!activeEntity || !activeChanges) return;
        await save(activeEntity, activeChanges);
    }, [activeChanges, activeEntity, save]);

    const submitPanelDelete = useCallback(async () => {
        if (!activeEntity) return;
        await remove(activeEntity);
        closeEntityPanel();
    }, [activeEntity, closeEntityPanel, remove]);

    const runPanelExtraAction = useCallback(async () => {
        if (!activeEntity) return;
        await runExtraAction(activeEntity);
    }, [activeEntity, runExtraAction]);

    const closeCreate = useCallback(() => {
        setShowCreate(false);
        setCreateRequest(props.initialCreate());
    }, [props]);

    const panelDirty = useMemo(() => {
        if (!activeEntity || !activeChanges) return false;
        return props.editFields.some(
            (field) =>
                fieldValue(activeChanges, field.key) !==
                fieldValue(props.changesFromEntity(activeEntity), field.key),
        );
    }, [activeChanges, activeEntity, props]);

    const panelContext = activeEntity ? (
        <Frame>
            <FrameHeader>
                <FrameTitle>Record details</FrameTitle>
                <FrameDescription>Read-only data from this record.</FrameDescription>
            </FrameHeader>
            <FramePanel>
                <dl data-slot="record-facts">
                    {props.displayFields.map((field) => (
                        <div key={field.key}>
                            <dt>{field.label}</dt>
                            <dd>{displayValue(asRecord(activeEntity)[field.key])}</dd>
                        </div>
                    ))}
                    {asRecord(activeEntity).createdAt !== undefined && (
                        <div>
                            <dt>Created</dt>
                            <dd>{displayValue(asRecord(activeEntity).createdAt)}</dd>
                        </div>
                    )}
                    {asRecord(activeEntity).updatedAt !== undefined && (
                        <div>
                            <dt>Updated</dt>
                            <dd>{displayValue(asRecord(activeEntity).updatedAt)}</dd>
                        </div>
                    )}
                </dl>
            </FramePanel>
        </Frame>
    ) : null;

    const panelActivity = activeEntity ? (
        <RecordActivity
            entityId={props.idOf(activeEntity)}
            entityName={activityEntityName(props.basePath)}
        />
    ) : null;

    return (
        <section>
            <header className="page-header">
                <div>
                    <p className="eyebrow">{props.eyebrow}</p>
                    <h2>{props.title}</h2>
                    <p>
                        {history.data
                            ? `${history.data.totalCount} records`
                            : `Loading …`}
                    </p>
                </div>
            </header>
            <section
                className="toolbar"
                aria-label={`${props.title} list toolbar`}
            >
                <label>
                    Search {props.title}
                    <Input
                        type="search"
                        aria-label={`Search ${props.title}`}
                        placeholder={`Search ${props.title}`}
                        value={search}
                        onChange={(event) => setSearch(event.target.value)}
                    />
                </label>
                <div aria-label="Selection">{selectedCount} selected</div>
                <div aria-label="Record count">
                    {searchableEntities.length} of{" "}
                    {history.data ? history.data.totalCount : 0} records
                </div>
                <Button type="button" onClick={() => setShowCreate(true)}>
                    Create {props.title}
                </Button>
            </section>
            {props.feedback.error && (
                <p role="alert" className="error-banner">
                    {props.feedback.error}
                </p>
            )}
            {history.isError && (
                <div className="error-banner" role="alert">
                    <span>Unable to load {props.title.toLowerCase()}.</span>
                    <Button intent="secondary" size="sm" type="button" onClick={() => void history.refetch()}>
                        Retry
                    </Button>
                </div>
            )}
            {!history.isError && (
                <VirtualizedDataTable
                    data={searchableEntities}
                    columns={columns}
                    getRowId={props.idOf}
                    onRowOpen={openEntityPanel}
                    selectedRowIds={selectedRowIds}
                    onSelectedRowIdsChange={onSelectedRowIdsChange}
                    testId={`virtual-${props.title.replace(/\s+/g, "-").toLowerCase()}-grid`}
                />
            )}
            {history.data && (
                <nav className="toolbar" aria-label={`${props.title} pages`}>
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
                description={`Add a ${props.title.toLowerCase()} record and its operational details.`}
                onOpenChange={(open) => { if (!open) closeCreate(); }}
                open={showCreate}
                title={`Create ${props.title}`}
            >
                    <ValidatedForm
                        schema={createSchema}
                        values={createRequest}
                        onValid={submitCreate}
                    >
                        {props.createFields.map((field) => (
                            <FieldInput
                                key={field.key}
                                field={field}
                                value={createRequest}
                                onChange={setCreateRequest}
                                contractOptions={
                                    field.key === "contractId"
                                        ? contractOptions
                                        : undefined
                                }
                            />
                        ))}
                        <div data-slot="entity-create-drawer-actions" className="toolbar">
                            <Button
                                intent="secondary"
                                type="button"
                                onClick={closeCreate}
                            >
                                Close
                            </Button>
                            <Button
                                type="submit"
                                disabled={props.createMutation.isPending}
                            >
                                Create
                            </Button>
                        </div>
                    </ValidatedForm>
            </EntityCreateDrawer>
            {activeEntity && activeChanges && (
                <RecordDetailPanel
                    open={Boolean(activeEntity)}
                    onOpenChange={(open) => {
                        if (!open) closeEntityPanel();
                    }}
                    eyebrow={props.eyebrow}
                    title={`${props.title} ${props.labelOf(activeEntity)}`}
                    description={`Update ${props.labelOf(activeEntity)}.`}
                    recordId={props.idOf(activeEntity)}
                    version={activeEntity.version}
                    dirty={panelDirty}
                    properties={(
                        <div data-slot="record-field-grid">
                            {props.editFields.map((field) => (
                                <div key={field.key} data-slot="record-field">
                                    <FieldInput
                                        field={field}
                                        value={activeChanges}
                                        onChange={setActiveChanges}
                                        contractOptions={
                                            field.key === "contractId"
                                                ? contractOptions
                                                : undefined
                                        }
                                    />
                                </div>
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
                            {props.extraAction && (
                                <Button
                                    intent="secondary"
                                    type="button"
                                    disabled={props.extraAction.disabled?.(
                                        activeEntity,
                                    )}
                                    onClick={() => void runPanelExtraAction()}
                                >
                                    {props.extraAction.label}
                                </Button>
                            )}
                            <Button
                                intent="danger"
                                type="button"
                                onClick={() => void submitPanelDelete()}
                            >
                                {props.cancelInsteadOfDelete
                                    ? "Cancel"
                                    : "Delete"}
                            </Button>
                        </>
                    )}
                />
            )}
            {props.feedback.conflict && (
                <div className="modal">
                    <ConflictDialog
                        entityId={props.feedback.conflict.id}
                        serverState={props.feedback.conflict.serverState}
                        attemptedChanges={props.feedback.conflict.attempted}
                        onClose={() => props.feedback.setConflict(undefined)}
                    />
                </div>
            )}
        </section>
    );
}

type CapacityChanges = Omit<
    UpdateCapacityBookingRequest,
    "capacityBookingId" | "version"
>;
export function CapacityBookingsPage() {
    const feedback = useFeedback<CapacityBookingDetailsDto>(
        "The capacity-booking change could not be saved.",
    );
    const createMutation = useCreateCapacityBooking(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateCapacityBooking(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteCapacityBooking(
        feedback.onConflict,
        feedback.onError,
    );
    return (
        <DomainCrudPage
            title="Capacity bookings"
            eyebrow="Transport"
            basePath="/api/v1/capacity-bookings"
            queryKey={domainQueryKeys.capacityBookings}
            idOf={(row) => row.capacityBookingId}
            labelOf={(row) => row.contractInstanceId}
            initialCreate={() => ({
                contractId: "",
                supplyMonth: monthStart(),
            })}
            createFromEntity={(row) => ({
                contractId: row.contractId,
                supplyMonth: row.supplyMonth,
                contractInstanceId: row.contractInstanceId,
                counterpartyId: row.counterpartyId,
                balancingGroup: row.balancingGroup,
                priceMechanism: row.priceMechanism,
                startArea: row.startArea,
                endArea: row.endArea,
                shipFix: row.shipFix,
                borderPoint: row.borderPoint,
                startDay: row.startDay,
                endDay: row.endDay,
                capacityMw: row.capacityMw,
                capacityPriceEurMwh: row.capacityPriceEurMwh,
                capacityCostEur: row.capacityCostEur,
                comments: row.comments,
            })}
            changesFromEntity={(row): CapacityChanges => ({
                balancingGroup: row.balancingGroup,
                priceMechanism: row.priceMechanism,
                startArea: row.startArea,
                endArea: row.endArea,
                startDay: row.startDay,
                endDay: row.endDay,
                capacityMw: row.capacityMw,
                capacityPriceEurMwh: row.capacityPriceEurMwh,
                capacityCostEur: row.capacityCostEur,
                comments: row.comments,
            })}
            createFields={[
                { key: "contractId", label: "Contract ID", required: true },
                {
                    key: "supplyMonth",
                    label: "Supply month",
                    kind: "date",
                    required: true,
                },
                { key: "contractInstanceId", label: "Contract instance" },
                { key: "startArea", label: "Start area" },
                { key: "endArea", label: "End area" },
                { key: "capacityMw", label: "Capacity MW", kind: "number" },
                {
                    key: "capacityPriceEurMwh",
                    label: "Capacity price EUR/MWh",
                    kind: "number",
                },
                { key: "comments", label: "Comments" },
            ]}
            editFields={[
                { key: "startArea", label: "Start area" },
                { key: "endArea", label: "End area" },
                { key: "capacityMw", label: "Capacity MW", kind: "number" },
                {
                    key: "capacityPriceEurMwh",
                    label: "Capacity price EUR/MWh",
                    kind: "number",
                },
                { key: "comments", label: "Comments" },
            ]}
            displayFields={[
                { key: "contractInstanceId", label: "Contract instance" },
                { key: "supplyMonth", label: "Month" },
                { key: "startArea", label: "From" },
                { key: "endArea", label: "To" },
                { key: "capacityMw", label: "MW" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
        />
    );
}

type TransferChanges = Omit<UpdateTransferRequest, "transferId" | "version">;
export function TransfersPage() {
    const feedback = useFeedback<TransferDetailsDto>(
        "The transfer change could not be saved.",
    );
    const createMutation = useCreateTransfer(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateTransfer(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteTransfer(
        feedback.onConflict,
        feedback.onError,
    );
    const changes = (row: TransferDetailsDto): TransferChanges => ({
        tradingArea: row.tradingArea,
        capacityMw: row.capacityMw,
        bookedCapacityMw: row.bookedCapacityMw,
        volumeMwh: row.volumeMwh,
        balancingEffectMwh: row.balancingEffectMwh,
        priceMechanism: row.priceMechanism,
        transportCostEurMwh: row.transportCostEurMwh,
        capacityCostEurMwh: row.capacityCostEurMwh,
        status: row.status,
        comments: row.comments,
    });
    return (
        <DomainCrudPage
            title="Transfers"
            eyebrow="Transport"
            basePath="/api/v1/transfers"
            queryKey={domainQueryKeys.transfers}
            idOf={(row) => row.transferId}
            labelOf={(row) => row.contractInstanceId}
            initialCreate={() => ({
                contractId: "",
                supplyMonth: monthStart(),
                status: "Pending - No Invoice",
            })}
            createFromEntity={(row) => ({
                contractId: row.contractId,
                supplyMonth: row.supplyMonth,
                contractInstanceId: row.contractInstanceId,
                counterpartyId: row.counterpartyId,
                balancingGroup: row.balancingGroup,
                ...changes(row),
            })}
            changesFromEntity={changes}
            createFields={[
                { key: "contractId", label: "Contract ID", required: true },
                {
                    key: "supplyMonth",
                    label: "Supply month",
                    kind: "date",
                    required: true,
                },
                { key: "contractInstanceId", label: "Contract instance" },
                { key: "tradingArea", label: "Trading area" },
                { key: "capacityMw", label: "Capacity MW", kind: "number" },
                { key: "volumeMwh", label: "Volume MWh", kind: "number" },
                { key: "status", label: "Status", options: reportStatuses },
                { key: "comments", label: "Comments" },
            ]}
            editFields={[
                { key: "tradingArea", label: "Trading area" },
                { key: "capacityMw", label: "Capacity MW", kind: "number" },
                { key: "volumeMwh", label: "Volume MWh", kind: "number" },
                { key: "status", label: "Status", options: reportStatuses },
                { key: "comments", label: "Comments" },
            ]}
            displayFields={[
                { key: "contractInstanceId", label: "Contract instance" },
                { key: "supplyMonth", label: "Month" },
                { key: "tradingArea", label: "Area" },
                { key: "volumeMwh", label: "MWh" },
                { key: "status", label: "Status" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
            cancelInsteadOfDelete
        />
    );
}

type BioticketChanges = Omit<UpdateBioticketRequest, "bioticketId" | "version">;
export function BioticketsPage() {
    const feedback = useFeedback<BioticketDetailsDto>(
        "The bioticket change could not be saved.",
    );
    const createMutation = useCreateBioticket(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateBioticket(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteBioticket(
        feedback.onConflict,
        feedback.onError,
    );
    const changes = (row: BioticketDetailsDto): BioticketChanges => ({
        volumeRealisedTon: row.volumeRealisedTon,
        volumeTon: row.volumeTon,
        costEurTon: row.costEurTon,
        revenueEur: row.revenueEur,
        vatPct: row.vatPct,
        vatEur: row.vatEur,
        invoiceAmountEur: row.invoiceAmountEur,
        status: row.status,
        comment: row.comment,
    });
    return (
        <DomainCrudPage
            title="Biotickets"
            eyebrow="Certificates"
            basePath="/api/v1/biotickets"
            queryKey={domainQueryKeys.biotickets}
            idOf={(row) => row.bioticketId}
            labelOf={(row) => row.contractInstanceId}
            initialCreate={() => ({
                contractId: "",
                bookType: "Sourcing",
                contractMonth: monthStart(),
                status: "Pending - No Invoice",
            })}
            createFromEntity={(row) => ({
                contractId: row.contractId,
                bookType: row.bookType,
                contractMonth: row.contractMonth,
                contractInstanceId: row.contractInstanceId,
                startDay: row.startDay,
                endDay: row.endDay,
                volumeNominatedTon: row.volumeNominatedTon,
                ...changes(row),
            })}
            changesFromEntity={changes}
            createFields={[
                { key: "contractId", label: "Contract ID", required: true },
                {
                    key: "bookType",
                    label: "Book type",
                    required: true,
                    options: ["Sourcing", "Sales"],
                },
                {
                    key: "contractMonth",
                    label: "Contract month",
                    kind: "date",
                    required: true,
                },
                { key: "contractInstanceId", label: "Contract instance" },
                {
                    key: "volumeRealisedTon",
                    label: "Realised tonnes",
                    kind: "number",
                },
                { key: "costEurTon", label: "Cost EUR/t", kind: "number" },
                { key: "status", label: "Status", options: reportStatuses },
                { key: "comment", label: "Comment" },
            ]}
            editFields={[
                {
                    key: "volumeRealisedTon",
                    label: "Realised tonnes",
                    kind: "number",
                },
                { key: "costEurTon", label: "Cost EUR/t", kind: "number" },
                { key: "status", label: "Status", options: reportStatuses },
                { key: "comment", label: "Comment" },
            ]}
            displayFields={[
                { key: "contractInstanceId", label: "Contract instance" },
                { key: "contractMonth", label: "Month" },
                { key: "bookType", label: "Book" },
                { key: "volumeTon", label: "Tonnes" },
                { key: "status", label: "Status" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
            cancelInsteadOfDelete
        />
    );
}

type GooChanges = Omit<
    UpdateGooCertificateTransactionRequest,
    "gooCertificateTransactionId" | "version"
>;
export function GooCertificatesPage() {
    const feedback = useFeedback<GooCertificateTransactionDetailsDto>(
        "The GoO transaction change could not be saved.",
    );
    const createMutation = useCreateGooCertificate(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateGooCertificate(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteGooCertificate(
        feedback.onConflict,
        feedback.onError,
    );
    const exportMutation = useRequestGooBatchExport(
        feedback.onConflict,
        feedback.onError,
    );
    const changes = (row: GooCertificateTransactionDetailsDto): GooChanges => ({
        batchType: row.batchType,
        producerContractId: row.producerContractId,
        customerContractId: row.customerContractId,
        register: row.register,
        status: row.status,
        transactionStartDate: row.transactionStartDate,
        transactionVolumeMwh: row.transactionVolumeMwh,
        volumeMwh: row.volumeMwh,
        text: row.text,
    });
    return (
        <DomainCrudPage
            title="GoO transactions"
            eyebrow="Certificates"
            basePath="/api/v1/goo-certificates"
            queryKey={domainQueryKeys.gooCertificates}
            idOf={(row) => row.gooCertificateTransactionId}
            labelOf={(row) =>
                row.transactionName ?? row.gooCertificateTransactionId
            }
            initialCreate={() => ({
                status: "Latest transaction",
                transactionStartDate: today(),
            })}
            createFromEntity={(row) => ({
                salesforceTransactionId: row.salesforceTransactionId,
                transactionName: row.transactionName,
                batchType: row.batchType,
                certificateTransactionId: row.certificateTransactionId,
                countryOfProduction: row.countryOfProduction,
                producerContractId: row.producerContractId,
                producerCompany: row.producerCompany,
                producerGooPriceEurMwh: row.producerGooPriceEurMwh,
                productionDate: row.productionDate,
                customerContractId: row.customerContractId,
                customerCompany: row.customerCompany,
                register: row.register,
                status: row.status,
                transactionStartDate: row.transactionStartDate,
                transactionVolumeMwh: row.transactionVolumeMwh,
                volumeMwh: row.volumeMwh,
                energySource: row.energySource,
                text: row.text,
            })}
            changesFromEntity={changes}
            createFields={[
                { key: "transactionName", label: "Transaction name" },
                { key: "producerContractId", label: "Producer contract ID" },
                { key: "customerContractId", label: "Customer contract ID" },
                {
                    key: "transactionStartDate",
                    label: "Transaction date",
                    kind: "date",
                },
                {
                    key: "transactionVolumeMwh",
                    label: "Transaction MWh",
                    kind: "number",
                },
                { key: "energySource", label: "Energy source" },
                {
                    key: "status",
                    label: "Status",
                    options: certificateStatuses,
                },
                { key: "text", label: "Notes" },
            ]}
            editFields={[
                { key: "producerContractId", label: "Producer contract ID" },
                { key: "customerContractId", label: "Customer contract ID" },
                {
                    key: "transactionVolumeMwh",
                    label: "Transaction MWh",
                    kind: "number",
                },
                {
                    key: "status",
                    label: "Status",
                    options: certificateStatuses,
                },
                { key: "text", label: "Notes" },
            ]}
            displayFields={[
                { key: "transactionName", label: "Transaction" },
                { key: "transactionStartDate", label: "Date" },
                { key: "energySource", label: "Source" },
                { key: "transactionVolumeMwh", label: "MWh" },
                { key: "status", label: "Status" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
            validateCreate={(request) =>
                request.producerContractId || request.customerContractId
                    ? undefined
                    : "A producer or customer contract ID is required."
            }
            extraAction={{
                label: "Request batch export",
                disabled: (row) =>
                    row.status === "Batch export requested" ||
                    row.status === "Processing",
                run: (row) =>
                    exportMutation.mutateAsync({
                        gooCertificateTransactionId:
                            row.gooCertificateTransactionId,
                        version: row.version,
                    }),
            }}
        />
    );
}

type TaxChanges = Omit<UpdateTaxTariffRequest, "taxTariffId" | "version">;
export function TaxTariffsPage() {
    const feedback = useFeedback<TaxTariffDetailsDto>(
        "The tax/tariff change could not be saved.",
    );
    const createMutation = useCreateTaxTariff(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateTaxTariff(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteTaxTariff(
        feedback.onConflict,
        feedback.onError,
    );
    const changes = (row: TaxTariffDetailsDto): TaxChanges => ({
        taxLocalCurMwh: row.taxLocalCurMwh,
        tsoLocalCurMwh: row.tsoLocalCurMwh,
        dsoLocalCurMwh: row.dsoLocalCurMwh,
        dsoTariffLocalCurDay: row.dsoTariffLocalCurDay,
        admFeeLocalCurMwh: row.admFeeLocalCurMwh,
        balFeeLocalCurMwh: row.balFeeLocalCurMwh,
        currency: row.currency,
    });
    return (
        <DomainCrudPage
            title="Taxes and tariffs"
            eyebrow="Master data"
            basePath="/api/v1/tax-tariffs"
            queryKey={domainQueryKeys.taxTariffs}
            idOf={(row) => row.taxTariffId}
            labelOf={(row) => `${row.contractId} ${row.periodStart}`}
            initialCreate={() => ({
                contractId: "",
                periodStart: today(),
                periodEnd: today(),
                currency: "EUR",
            })}
            createFromEntity={(row) => ({
                contractId: row.contractId,
                counterpartyId: row.counterpartyId,
                periodStart: row.periodStart,
                periodEnd: row.periodEnd,
                ...changes(row),
            })}
            changesFromEntity={changes}
            createFields={[
                { key: "contractId", label: "Contract ID", required: true },
                {
                    key: "periodStart",
                    label: "Period start",
                    kind: "date",
                    required: true,
                },
                {
                    key: "periodEnd",
                    label: "Period end",
                    kind: "date",
                    required: true,
                },
                { key: "taxLocalCurMwh", label: "Tax / MWh", kind: "number" },
                { key: "tsoLocalCurMwh", label: "TSO / MWh", kind: "number" },
                { key: "dsoLocalCurMwh", label: "DSO / MWh", kind: "number" },
                { key: "currency", label: "Currency", required: true },
            ]}
            editFields={[
                { key: "taxLocalCurMwh", label: "Tax / MWh", kind: "number" },
                { key: "tsoLocalCurMwh", label: "TSO / MWh", kind: "number" },
                { key: "dsoLocalCurMwh", label: "DSO / MWh", kind: "number" },
                { key: "currency", label: "Currency", required: true },
            ]}
            displayFields={[
                { key: "contractId", label: "Contract" },
                { key: "periodStart", label: "From" },
                { key: "periodEnd", label: "To" },
                { key: "taxLocalCurMwh", label: "Tax/MWh" },
                { key: "currency", label: "Currency" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
        />
    );
}

type HedgeChanges = Omit<UpdateHedgeRequest, "hedgeId" | "version">;
export function HedgesPage() {
    const feedback = useFeedback<HedgeDetailsDto>(
        "The hedge change could not be saved.",
    );
    const createMutation = useCreateHedge(
        feedback.onConflict,
        feedback.onError,
    );
    const updateMutation = useUpdateHedge(
        feedback.onConflict,
        feedback.onError,
    );
    const deleteMutation = useDeleteHedge(
        feedback.onConflict,
        feedback.onError,
    );
    const changes = (row: HedgeDetailsDto): HedgeChanges => ({
        hedgeAmountMwh: row.hedgeAmountMwh,
        hedgePriceEurMwh: row.hedgePriceEurMwh,
    });
    return (
        <DomainCrudPage
            title="Hedges"
            eyebrow="Risk"
            basePath="/api/v1/hedges"
            queryKey={domainQueryKeys.hedges}
            idOf={(row) => row.hedgeId}
            labelOf={(row) => `${row.contractId} ${row.month}`}
            initialCreate={() => ({ contractId: "", month: monthStart() })}
            createFromEntity={(row) => ({
                contractId: row.contractId,
                month: row.month,
                ...changes(row),
            })}
            changesFromEntity={changes}
            createFields={[
                { key: "contractId", label: "Contract ID", required: true },
                { key: "month", label: "Month", kind: "date", required: true },
                { key: "hedgeAmountMwh", label: "Amount MWh", kind: "number" },
                {
                    key: "hedgePriceEurMwh",
                    label: "Price EUR/MWh",
                    kind: "number",
                },
            ]}
            editFields={[
                { key: "hedgeAmountMwh", label: "Amount MWh", kind: "number" },
                {
                    key: "hedgePriceEurMwh",
                    label: "Price EUR/MWh",
                    kind: "number",
                },
            ]}
            displayFields={[
                { key: "contractId", label: "Contract" },
                { key: "month", label: "Month" },
                { key: "hedgeAmountMwh", label: "MWh" },
                { key: "hedgePriceEurMwh", label: "EUR/MWh" },
            ]}
            createMutation={createMutation}
            updateMutation={updateMutation}
            deleteMutation={deleteMutation}
            feedback={feedback}
        />
    );
}
