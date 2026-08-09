import { useQuery } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { useCallback, useMemo, useRef, useState } from 'react';
import { z } from 'zod';
import type { BioticketDetailsDto } from '../../api/generated/bioticket-details-dto';
import type { CapacityBookingDetailsDto } from '../../api/generated/capacity-booking-details-dto';
import type { CreateBioticketRequest } from '../../api/generated/create-bioticket-request';
import type { CreateCapacityBookingRequest } from '../../api/generated/create-capacity-booking-request';
import type { CreateGooCertificateTransactionRequest } from '../../api/generated/create-goo-certificate-transaction-request';
import type { CreateHedgeRequest } from '../../api/generated/create-hedge-request';
import type { CreateTaxTariffRequest } from '../../api/generated/create-tax-tariff-request';
import type { CreateTransferRequest } from '../../api/generated/create-transfer-request';
import type { GooCertificateTransactionDetailsDto } from '../../api/generated/goo-certificate-transaction-details-dto';
import type { HedgeDetailsDto } from '../../api/generated/hedge-details-dto';
import type { TaxTariffDetailsDto } from '../../api/generated/tax-tariff-details-dto';
import type { TransferDetailsDto } from '../../api/generated/transfer-details-dto';
import type { UpdateBioticketRequest } from '../../api/generated/update-bioticket-request';
import type { UpdateCapacityBookingRequest } from '../../api/generated/update-capacity-booking-request';
import type { UpdateGooCertificateTransactionRequest } from '../../api/generated/update-goo-certificate-transaction-request';
import type { UpdateHedgeRequest } from '../../api/generated/update-hedge-request';
import type { UpdateTaxTariffRequest } from '../../api/generated/update-tax-tariff-request';
import type { UpdateTransferRequest } from '../../api/generated/update-transfer-request';
import { apiFetch } from '../../lib/api/client';
import { useCommandStack } from '../../lib/commands/CommandStackContext';
import type { Command } from '../../lib/commands/UndoRedoStack';
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
  useUpdateTransfer
} from '../../lib/mutations/domainEntityMutations';
import { listQueryKey } from '../../lib/query/queryKeys';
import { VirtualizedDataTable } from '../grid/VirtualizedDataTable';
import { ConflictDialog } from '../ui/ConflictDialog';
import { ValidatedForm } from '../ui/validated-form';

type Versioned = { version: number };
type InputKind = 'text' | 'number' | 'date';
interface FieldSpec { key: string; label: string; kind?: InputKind; required?: boolean; options?: readonly string[]; }
interface MutationLike<TResult, TVariables> { mutateAsync: (variables: TVariables) => Promise<TResult>; isPending: boolean; }
interface Feedback<T> {
  attempted: { current: object };
  error: string;
  setError: (message: string) => void;
  conflict?: { id: string; serverState?: T; attempted: object };
  setConflict: (value: { id: string; serverState?: T; attempted: object } | undefined) => void;
  onConflict: (id: string, serverState?: T) => void;
  onError: (failure: unknown) => void;
}

const reportStatuses = ['Completed - Payment Received/Sent', 'In Progress - Invoice Received/Sent', 'Pending - No Invoice', 'Cancelled', 'Awaiting', 'Issue'] as const;
const certificateStatuses = ['Latest transaction', 'Batch export requested', 'Processing', 'Completed', 'Failed'] as const;
const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => `${new Date().toISOString().slice(0, 7)}-01`;

function asRecord(value: object): Record<string, unknown> { return value as Record<string, unknown>; }
function fieldValue(value: object, key: string): string { const raw = asRecord(value)[key]; return raw === null || raw === undefined ? '' : String(raw); }
function changedValue(raw: string, field: FieldSpec): unknown { return field.kind === 'number' ? (raw === '' ? null : Number(raw)) : (raw === '' && !field.required ? null : raw); }
function changeField<T extends object>(value: T, field: FieldSpec, raw: string): T { return { ...value, [field.key]: changedValue(raw, field) }; }
function displayValue(value: unknown): string { return value === null || value === undefined || value === '' ? '—' : String(value); }

function FieldInput<T extends object>({ field, value, onChange }: { field: FieldSpec; value: T; onChange: (next: T) => void }) {
  const current = fieldValue(value, field.key);
  if (field.options) {
    return <label>{field.label}<select required={field.required} value={current} onChange={(event) => onChange(changeField(value, field, event.target.value))}>{!field.required && <option value="">Not set</option>}{field.options.map((option) => <option key={option}>{option}</option>)}</select></label>;
  }
  return <label>{field.label}<input required={field.required} type={field.kind ?? 'text'} step={field.kind === 'number' ? 'any' : undefined} value={current} onChange={(event) => onChange(changeField(value, field, event.target.value))} /></label>;
}

function useFeedback<T>(fallback: string): Feedback<T> {
  const attempted = useRef<object>({});
  const [error, setError] = useState('');
  const [conflict, setConflict] = useState<Feedback<T>['conflict']>();
  const onConflict = useCallback((id: string, serverState?: T) => setConflict({ id, serverState, attempted: attempted.current }), []);
  const onError = useCallback((failure: unknown) => setError(failure instanceof Error ? failure.message : fallback), [fallback]);
  return { attempted, error, setError, conflict, setConflict, onConflict, onError };
}

interface CrudPageProps<T extends Versioned, TCreate extends object, TChanges extends object> {
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
  extraAction?: { label: string; run: (entity: T) => Promise<T>; disabled?: (entity: T) => boolean };
}

function EntityEditor<TChanges extends object>({ initialChanges, fields, onSave, onDelete, deleteLabel, extraAction }: {
  initialChanges: TChanges;
  fields: FieldSpec[];
  onSave: (changes: TChanges) => void;
  onDelete: () => void;
  deleteLabel: string;
  extraAction?: { label: string; run: () => void; disabled?: boolean };
}) {
  const initial = useMemo(() => ({ ...initialChanges }), [initialChanges]);
  const [changes, setChanges] = useState(initial);
  return <details><summary>Actions</summary><div className="row-actions">{fields.map((field) => <FieldInput key={field.key} field={field} value={changes} onChange={setChanges} />)}<button type="button" onClick={() => onSave(changes)}>Save</button>{extraAction && <button type="button" disabled={extraAction.disabled} onClick={extraAction.run}>{extraAction.label}</button>}<button type="button" className="danger" onClick={onDelete}>{deleteLabel}</button></div></details>;
}

function DomainCrudPage<T extends Versioned, TCreate extends object, TChanges extends object>(props: CrudPageProps<T, TCreate, TChanges>) {
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [createRequest, setCreateRequest] = useState<TCreate>(props.initialCreate);
  const createSchema = useMemo(() => z.custom<TCreate>((candidate) => {
    if (typeof candidate !== 'object' || candidate === null) return false;
    const record = candidate as Record<string, unknown>;
    return props.createFields.every((field) => {
      const value = record[field.key];
      if (field.required && (value === undefined || value === null || value === '')) return false;
      return field.kind !== 'number' || value === undefined || value === null || (typeof value === 'number' && Number.isFinite(value));
    });
  }, { error: 'Complete the required fields with valid values.' }), [props.createFields]);
  const commandStack = useCommandStack();
  const history = useQuery({
    queryKey: listQueryKey(props.queryKey, { page, pageSize: 100 }),
    queryFn: ({ signal }) => apiFetch<PagedEntityCache<T>>(`${props.basePath}?page=${page}&pageSize=100`, { signal }),
  });

  const save = useCallback(async (entity: T, requested: TChanges) => {
    props.feedback.setError('');
    const changes = Object.fromEntries(props.editFields.map((field) => [field.key, asRecord(requested)[field.key]])) as TChanges;
    const before = props.changesFromEntity(entity);
    let current = entity;
    const command: Command = {
      id: crypto.randomUUID(), description: `Update ${props.labelOf(entity)}`, timestamp: Date.now(),
      execute: async () => { props.feedback.attempted.current = changes; current = await props.updateMutation.mutateAsync({ id: props.idOf(current), version: current.version, changes }); },
      undo: async () => { props.feedback.attempted.current = before; current = await props.updateMutation.mutateAsync({ id: props.idOf(current), version: current.version, changes: before }); }
    };
    try { await commandStack.execute(command); } catch { /* mutation callbacks own user-visible failures */ }
  }, [commandStack, props]);

  const remove = useCallback(async (entity: T) => {
    props.feedback.setError('');
    const reason = props.cancelInsteadOfDelete ? 'Cancelled from Tradebook UI' : 'Deleted from Tradebook UI';
    const restoreChanges = props.changesFromEntity(entity);
    let current = entity;
    const command: Command = {
      id: crypto.randomUUID(), description: `${props.cancelInsteadOfDelete ? 'Cancel' : 'Delete'} ${props.labelOf(entity)}`, timestamp: Date.now(),
      execute: async () => {
        props.feedback.attempted.current = { reason };
        await props.deleteMutation.mutateAsync({ id: props.idOf(current), version: current.version, reason });
        if (props.cancelInsteadOfDelete) current = await apiFetch<T>(`${props.basePath}/${encodeURIComponent(props.idOf(current))}`);
      },
      undo: async () => {
        props.feedback.attempted.current = restoreChanges;
        current = props.cancelInsteadOfDelete
          ? await props.updateMutation.mutateAsync({ id: props.idOf(current), version: current.version, changes: restoreChanges })
          : await props.createMutation.mutateAsync(props.createFromEntity(entity));
      }
    };
    try { await commandStack.execute(command); } catch { /* mutation callbacks own user-visible failures */ }
  }, [commandStack, props]);

  const runExtraAction = useCallback(async (entity: T) => {
    if (!props.extraAction) return;
    const before = props.changesFromEntity(entity);
    let current = entity;
    const command: Command = {
      id: crypto.randomUUID(), description: `${props.extraAction.label}: ${props.labelOf(entity)}`, timestamp: Date.now(),
      execute: async () => { props.feedback.attempted.current = { action: props.extraAction!.label }; current = await props.extraAction!.run(current); },
      undo: async () => { props.feedback.attempted.current = before; current = await props.updateMutation.mutateAsync({ id: props.idOf(current), version: current.version, changes: before }); }
    };
    try { await commandStack.execute(command); } catch { /* mutation callbacks own user-visible failures */ }
  }, [commandStack, props]);

  const columns = useMemo<ColumnDef<T>[]>(() => [
    ...props.displayFields.map((field): ColumnDef<T> => ({ id: field.key, header: field.label, accessorFn: (row) => asRecord(row)[field.key], cell: ({ getValue }) => displayValue(getValue()) })),
    { id: 'actions', header: 'Actions', cell: ({ row }) => <EntityEditor<TChanges> key={row.original.version} initialChanges={props.changesFromEntity(row.original)} fields={props.editFields} onSave={(changes) => void save(row.original, changes)} onDelete={() => void remove(row.original)} deleteLabel={props.cancelInsteadOfDelete ? 'Cancel' : 'Delete'} extraAction={props.extraAction ? { label: props.extraAction.label, disabled: props.extraAction.disabled?.(row.original), run: () => void runExtraAction(row.original) } : undefined} /> }
  ], [props, remove, runExtraAction, save]);

  const submitCreate = async (validatedRequest: TCreate) => {
    props.feedback.setError('');
    const request = { ...validatedRequest };
    const validationError = props.validateCreate?.(request);
    if (validationError) { props.feedback.setError(validationError); return; }
    let current: T | undefined;
    let restoreChanges: TChanges | undefined;
    const command: Command = {
      id: crypto.randomUUID(), description: `Create ${props.title}`, timestamp: Date.now(),
      execute: async () => {
        props.feedback.attempted.current = request;
        if (props.cancelInsteadOfDelete && current && restoreChanges) {
          current = await props.updateMutation.mutateAsync({ id: props.idOf(current), version: current.version, changes: restoreChanges });
        } else {
          current = await props.createMutation.mutateAsync(request);
          restoreChanges = props.changesFromEntity(current);
        }
      },
      undo: async () => {
        if (!current) return;
        await props.deleteMutation.mutateAsync({ id: props.idOf(current), version: current.version, reason: 'Undo create' });
        if (props.cancelInsteadOfDelete) current = await apiFetch<T>(`${props.basePath}/${encodeURIComponent(props.idOf(current))}`);
      }
    };
    try { await commandStack.execute(command); setCreateRequest(props.initialCreate()); setShowCreate(false); } catch { /* mutation callbacks own user-visible failures */ }
  };

  return <section><header className="page-header"><div><p className="eyebrow">{props.eyebrow}</p><h2>{props.title}</h2><p>{history.data ? `${history.data.totalCount} records` : `Loading ${props.title.toLowerCase()}…`}</p></div><button type="button" onClick={() => setShowCreate(true)}>Create</button></header>{props.feedback.error && <p role="alert" className="error-banner">{props.feedback.error}</p>}{history.isError && <p role="alert">Unable to load {props.title.toLowerCase()}.</p>}{!history.isError && <VirtualizedDataTable data={history.data?.items ?? []} columns={columns} getRowId={props.idOf} />}{history.data && <nav className="toolbar" aria-label={`${props.title} pages`}><button type="button" disabled={page === 1 || history.isFetching} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button><span>Page {page}</span><button type="button" disabled={!history.data.hasNextPage || history.isFetching} onClick={() => setPage((value) => value + 1)}>Next</button></nav>}{showCreate && <section className="modal" role="dialog" aria-modal="true" aria-label={`Create ${props.title}`}><ValidatedForm schema={createSchema} values={createRequest} onValid={submitCreate}><h3>Create {props.title}</h3>{props.createFields.map((field) => <FieldInput key={field.key} field={field} value={createRequest} onChange={setCreateRequest} />)}<div className="toolbar"><button type="button" className="secondary" onClick={() => setShowCreate(false)}>Close</button><button type="submit" disabled={props.createMutation.isPending}>Create</button></div></ValidatedForm></section>}{props.feedback.conflict && <div className="modal"><ConflictDialog entityId={props.feedback.conflict.id} serverState={props.feedback.conflict.serverState} attemptedChanges={props.feedback.conflict.attempted} onClose={() => props.feedback.setConflict(undefined)} /></div>}</section>;
}

type CapacityChanges = Omit<UpdateCapacityBookingRequest, 'capacityBookingId' | 'version'>;
export function CapacityBookingsPage() {
  const feedback = useFeedback<CapacityBookingDetailsDto>('The capacity-booking change could not be saved.');
  const createMutation = useCreateCapacityBooking(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateCapacityBooking(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteCapacityBooking(feedback.onConflict, feedback.onError);
  return <DomainCrudPage title="Capacity bookings" eyebrow="Transport" basePath="/api/v1/capacity-bookings" queryKey={domainQueryKeys.capacityBookings} idOf={(row) => row.capacityBookingId} labelOf={(row) => row.contractInstanceId} initialCreate={() => ({ contractId: '', supplyMonth: monthStart() })} createFromEntity={(row) => ({ contractId: row.contractId, supplyMonth: row.supplyMonth, contractInstanceId: row.contractInstanceId, counterpartyId: row.counterpartyId, balancingGroup: row.balancingGroup, priceMechanism: row.priceMechanism, startArea: row.startArea, endArea: row.endArea, shipFix: row.shipFix, borderPoint: row.borderPoint, startDay: row.startDay, endDay: row.endDay, capacityMw: row.capacityMw, capacityPriceEurMwh: row.capacityPriceEurMwh, capacityCostEur: row.capacityCostEur, comments: row.comments })} changesFromEntity={(row): CapacityChanges => ({ balancingGroup: row.balancingGroup, priceMechanism: row.priceMechanism, startArea: row.startArea, endArea: row.endArea, startDay: row.startDay, endDay: row.endDay, capacityMw: row.capacityMw, capacityPriceEurMwh: row.capacityPriceEurMwh, capacityCostEur: row.capacityCostEur, comments: row.comments })} createFields={[{ key: 'contractId', label: 'Contract ID', required: true }, { key: 'supplyMonth', label: 'Supply month', kind: 'date', required: true }, { key: 'contractInstanceId', label: 'Contract instance' }, { key: 'startArea', label: 'Start area' }, { key: 'endArea', label: 'End area' }, { key: 'capacityMw', label: 'Capacity MW', kind: 'number' }, { key: 'capacityPriceEurMwh', label: 'Capacity price EUR/MWh', kind: 'number' }, { key: 'comments', label: 'Comments' }]} editFields={[{ key: 'startArea', label: 'Start area' }, { key: 'endArea', label: 'End area' }, { key: 'capacityMw', label: 'Capacity MW', kind: 'number' }, { key: 'capacityPriceEurMwh', label: 'Capacity price EUR/MWh', kind: 'number' }, { key: 'comments', label: 'Comments' }]} displayFields={[{ key: 'contractInstanceId', label: 'Contract instance' }, { key: 'supplyMonth', label: 'Month' }, { key: 'startArea', label: 'From' }, { key: 'endArea', label: 'To' }, { key: 'capacityMw', label: 'MW' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} />;
}

type TransferChanges = Omit<UpdateTransferRequest, 'transferId' | 'version'>;
export function TransfersPage() {
  const feedback = useFeedback<TransferDetailsDto>('The transfer change could not be saved.');
  const createMutation = useCreateTransfer(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateTransfer(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteTransfer(feedback.onConflict, feedback.onError);
  const changes = (row: TransferDetailsDto): TransferChanges => ({ tradingArea: row.tradingArea, capacityMw: row.capacityMw, bookedCapacityMw: row.bookedCapacityMw, volumeMwh: row.volumeMwh, balancingEffectMwh: row.balancingEffectMwh, priceMechanism: row.priceMechanism, transportCostEurMwh: row.transportCostEurMwh, capacityCostEurMwh: row.capacityCostEurMwh, status: row.status, comments: row.comments });
  return <DomainCrudPage title="Transfers" eyebrow="Transport" basePath="/api/v1/transfers" queryKey={domainQueryKeys.transfers} idOf={(row) => row.transferId} labelOf={(row) => row.contractInstanceId} initialCreate={() => ({ contractId: '', supplyMonth: monthStart(), status: 'Pending - No Invoice' })} createFromEntity={(row) => ({ contractId: row.contractId, supplyMonth: row.supplyMonth, contractInstanceId: row.contractInstanceId, counterpartyId: row.counterpartyId, balancingGroup: row.balancingGroup, ...changes(row) })} changesFromEntity={changes} createFields={[{ key: 'contractId', label: 'Contract ID', required: true }, { key: 'supplyMonth', label: 'Supply month', kind: 'date', required: true }, { key: 'contractInstanceId', label: 'Contract instance' }, { key: 'tradingArea', label: 'Trading area' }, { key: 'capacityMw', label: 'Capacity MW', kind: 'number' }, { key: 'volumeMwh', label: 'Volume MWh', kind: 'number' }, { key: 'status', label: 'Status', options: reportStatuses }, { key: 'comments', label: 'Comments' }]} editFields={[{ key: 'tradingArea', label: 'Trading area' }, { key: 'capacityMw', label: 'Capacity MW', kind: 'number' }, { key: 'volumeMwh', label: 'Volume MWh', kind: 'number' }, { key: 'status', label: 'Status', options: reportStatuses }, { key: 'comments', label: 'Comments' }]} displayFields={[{ key: 'contractInstanceId', label: 'Contract instance' }, { key: 'supplyMonth', label: 'Month' }, { key: 'tradingArea', label: 'Area' }, { key: 'volumeMwh', label: 'MWh' }, { key: 'status', label: 'Status' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} cancelInsteadOfDelete />;
}

type BioticketChanges = Omit<UpdateBioticketRequest, 'bioticketId' | 'version'>;
export function BioticketsPage() {
  const feedback = useFeedback<BioticketDetailsDto>('The bioticket change could not be saved.');
  const createMutation = useCreateBioticket(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateBioticket(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteBioticket(feedback.onConflict, feedback.onError);
  const changes = (row: BioticketDetailsDto): BioticketChanges => ({ volumeRealisedTon: row.volumeRealisedTon, volumeTon: row.volumeTon, costEurTon: row.costEurTon, revenueEur: row.revenueEur, vatPct: row.vatPct, vatEur: row.vatEur, invoiceAmountEur: row.invoiceAmountEur, status: row.status, comment: row.comment });
  return <DomainCrudPage title="Biotickets" eyebrow="Certificates" basePath="/api/v1/biotickets" queryKey={domainQueryKeys.biotickets} idOf={(row) => row.bioticketId} labelOf={(row) => row.contractInstanceId} initialCreate={() => ({ contractId: '', bookType: 'Sourcing', contractMonth: monthStart(), status: 'Pending - No Invoice' })} createFromEntity={(row) => ({ contractId: row.contractId, bookType: row.bookType, contractMonth: row.contractMonth, contractInstanceId: row.contractInstanceId, startDay: row.startDay, endDay: row.endDay, volumeNominatedTon: row.volumeNominatedTon, ...changes(row) })} changesFromEntity={changes} createFields={[{ key: 'contractId', label: 'Contract ID', required: true }, { key: 'bookType', label: 'Book type', required: true, options: ['Sourcing', 'Sales'] }, { key: 'contractMonth', label: 'Contract month', kind: 'date', required: true }, { key: 'contractInstanceId', label: 'Contract instance' }, { key: 'volumeRealisedTon', label: 'Realised tonnes', kind: 'number' }, { key: 'costEurTon', label: 'Cost EUR/t', kind: 'number' }, { key: 'status', label: 'Status', options: reportStatuses }, { key: 'comment', label: 'Comment' }]} editFields={[{ key: 'volumeRealisedTon', label: 'Realised tonnes', kind: 'number' }, { key: 'costEurTon', label: 'Cost EUR/t', kind: 'number' }, { key: 'status', label: 'Status', options: reportStatuses }, { key: 'comment', label: 'Comment' }]} displayFields={[{ key: 'contractInstanceId', label: 'Contract instance' }, { key: 'contractMonth', label: 'Month' }, { key: 'bookType', label: 'Book' }, { key: 'volumeTon', label: 'Tonnes' }, { key: 'status', label: 'Status' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} cancelInsteadOfDelete />;
}

type GooChanges = Omit<UpdateGooCertificateTransactionRequest, 'gooCertificateTransactionId' | 'version'>;
export function GooCertificatesPage() {
  const feedback = useFeedback<GooCertificateTransactionDetailsDto>('The GoO transaction change could not be saved.');
  const createMutation = useCreateGooCertificate(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateGooCertificate(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteGooCertificate(feedback.onConflict, feedback.onError);
  const exportMutation = useRequestGooBatchExport(feedback.onConflict, feedback.onError);
  const changes = (row: GooCertificateTransactionDetailsDto): GooChanges => ({ batchType: row.batchType, producerContractId: row.producerContractId, customerContractId: row.customerContractId, register: row.register, status: row.status, transactionStartDate: row.transactionStartDate, transactionVolumeMwh: row.transactionVolumeMwh, volumeMwh: row.volumeMwh, text: row.text });
  return <DomainCrudPage title="GoO transactions" eyebrow="Certificates" basePath="/api/v1/goo-certificates" queryKey={domainQueryKeys.gooCertificates} idOf={(row) => row.gooCertificateTransactionId} labelOf={(row) => row.transactionName ?? row.gooCertificateTransactionId} initialCreate={() => ({ status: 'Latest transaction', transactionStartDate: today() })} createFromEntity={(row) => ({ salesforceTransactionId: row.salesforceTransactionId, transactionName: row.transactionName, batchType: row.batchType, certificateTransactionId: row.certificateTransactionId, countryOfProduction: row.countryOfProduction, producerContractId: row.producerContractId, producerCompany: row.producerCompany, producerGooPriceEurMwh: row.producerGooPriceEurMwh, productionDate: row.productionDate, customerContractId: row.customerContractId, customerCompany: row.customerCompany, register: row.register, status: row.status, transactionStartDate: row.transactionStartDate, transactionVolumeMwh: row.transactionVolumeMwh, volumeMwh: row.volumeMwh, energySource: row.energySource, text: row.text })} changesFromEntity={changes} createFields={[{ key: 'transactionName', label: 'Transaction name' }, { key: 'producerContractId', label: 'Producer contract ID' }, { key: 'customerContractId', label: 'Customer contract ID' }, { key: 'transactionStartDate', label: 'Transaction date', kind: 'date' }, { key: 'transactionVolumeMwh', label: 'Transaction MWh', kind: 'number' }, { key: 'energySource', label: 'Energy source' }, { key: 'status', label: 'Status', options: certificateStatuses }, { key: 'text', label: 'Notes' }]} editFields={[{ key: 'producerContractId', label: 'Producer contract ID' }, { key: 'customerContractId', label: 'Customer contract ID' }, { key: 'transactionVolumeMwh', label: 'Transaction MWh', kind: 'number' }, { key: 'status', label: 'Status', options: certificateStatuses }, { key: 'text', label: 'Notes' }]} displayFields={[{ key: 'transactionName', label: 'Transaction' }, { key: 'transactionStartDate', label: 'Date' }, { key: 'energySource', label: 'Source' }, { key: 'transactionVolumeMwh', label: 'MWh' }, { key: 'status', label: 'Status' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} validateCreate={(request) => request.producerContractId || request.customerContractId ? undefined : 'A producer or customer contract ID is required.'} extraAction={{ label: 'Request batch export', disabled: (row) => row.status === 'Batch export requested' || row.status === 'Processing', run: (row) => exportMutation.mutateAsync({ gooCertificateTransactionId: row.gooCertificateTransactionId, version: row.version }) }} />;
}

type TaxChanges = Omit<UpdateTaxTariffRequest, 'taxTariffId' | 'version'>;
export function TaxTariffsPage() {
  const feedback = useFeedback<TaxTariffDetailsDto>('The tax/tariff change could not be saved.');
  const createMutation = useCreateTaxTariff(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateTaxTariff(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteTaxTariff(feedback.onConflict, feedback.onError);
  const changes = (row: TaxTariffDetailsDto): TaxChanges => ({ taxLocalCurMwh: row.taxLocalCurMwh, tsoLocalCurMwh: row.tsoLocalCurMwh, dsoLocalCurMwh: row.dsoLocalCurMwh, dsoTariffLocalCurDay: row.dsoTariffLocalCurDay, admFeeLocalCurMwh: row.admFeeLocalCurMwh, balFeeLocalCurMwh: row.balFeeLocalCurMwh, currency: row.currency });
  return <DomainCrudPage title="Taxes and tariffs" eyebrow="Master data" basePath="/api/v1/tax-tariffs" queryKey={domainQueryKeys.taxTariffs} idOf={(row) => row.taxTariffId} labelOf={(row) => `${row.contractId} ${row.periodStart}`} initialCreate={() => ({ contractId: '', periodStart: today(), periodEnd: today(), currency: 'EUR' })} createFromEntity={(row) => ({ contractId: row.contractId, counterpartyId: row.counterpartyId, periodStart: row.periodStart, periodEnd: row.periodEnd, ...changes(row) })} changesFromEntity={changes} createFields={[{ key: 'contractId', label: 'Contract ID', required: true }, { key: 'periodStart', label: 'Period start', kind: 'date', required: true }, { key: 'periodEnd', label: 'Period end', kind: 'date', required: true }, { key: 'taxLocalCurMwh', label: 'Tax / MWh', kind: 'number' }, { key: 'tsoLocalCurMwh', label: 'TSO / MWh', kind: 'number' }, { key: 'dsoLocalCurMwh', label: 'DSO / MWh', kind: 'number' }, { key: 'currency', label: 'Currency', required: true }]} editFields={[{ key: 'taxLocalCurMwh', label: 'Tax / MWh', kind: 'number' }, { key: 'tsoLocalCurMwh', label: 'TSO / MWh', kind: 'number' }, { key: 'dsoLocalCurMwh', label: 'DSO / MWh', kind: 'number' }, { key: 'currency', label: 'Currency', required: true }]} displayFields={[{ key: 'contractId', label: 'Contract' }, { key: 'periodStart', label: 'From' }, { key: 'periodEnd', label: 'To' }, { key: 'taxLocalCurMwh', label: 'Tax/MWh' }, { key: 'currency', label: 'Currency' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} />;
}

type HedgeChanges = Omit<UpdateHedgeRequest, 'hedgeId' | 'version'>;
export function HedgesPage() {
  const feedback = useFeedback<HedgeDetailsDto>('The hedge change could not be saved.');
  const createMutation = useCreateHedge(feedback.onConflict, feedback.onError);
  const updateMutation = useUpdateHedge(feedback.onConflict, feedback.onError);
  const deleteMutation = useDeleteHedge(feedback.onConflict, feedback.onError);
  const changes = (row: HedgeDetailsDto): HedgeChanges => ({ hedgeAmountMwh: row.hedgeAmountMwh, hedgePriceEurMwh: row.hedgePriceEurMwh });
  return <DomainCrudPage title="Hedges" eyebrow="Risk" basePath="/api/v1/hedges" queryKey={domainQueryKeys.hedges} idOf={(row) => row.hedgeId} labelOf={(row) => `${row.contractId} ${row.month}`} initialCreate={() => ({ contractId: '', month: monthStart() })} createFromEntity={(row) => ({ contractId: row.contractId, month: row.month, ...changes(row) })} changesFromEntity={changes} createFields={[{ key: 'contractId', label: 'Contract ID', required: true }, { key: 'month', label: 'Month', kind: 'date', required: true }, { key: 'hedgeAmountMwh', label: 'Amount MWh', kind: 'number' }, { key: 'hedgePriceEurMwh', label: 'Price EUR/MWh', kind: 'number' }]} editFields={[{ key: 'hedgeAmountMwh', label: 'Amount MWh', kind: 'number' }, { key: 'hedgePriceEurMwh', label: 'Price EUR/MWh', kind: 'number' }]} displayFields={[{ key: 'contractId', label: 'Contract' }, { key: 'month', label: 'Month' }, { key: 'hedgeAmountMwh', label: 'MWh' }, { key: 'hedgePriceEurMwh', label: 'EUR/MWh' }]} createMutation={createMutation} updateMutation={updateMutation} deleteMutation={deleteMutation} feedback={feedback} />;
}
