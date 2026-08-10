import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import type { z } from 'zod';
import { Form } from './form';

export interface ValidatedFormProps<T extends object> {
  children: ReactNode;
  className?: string;
  schema: z.ZodType<T>;
  values: T;
  onValid: (values: T) => void | Promise<void>;
}

type FormValues = Record<string, unknown>;

/** Maps zod issues to [name, first message]; path-less issues (whole-object schemas) map to `root`. */
function failuresFromIssues(issues: readonly z.core.$ZodIssue[]): [string, string][] {
  const failures = new Map<string, string>();
  for (const issue of issues) {
    const name = issue.path.length > 0 ? issue.path.map(String).join('.') : 'root';
    if (!failures.has(name)) failures.set(name, issue.message);
  }
  return [...failures.entries()];
}

function sameFailures(a: [string, string][], b: [string, string][]): boolean {
  return a.length === b.length && a.every(([name, message], index) => b[index]?.[0] === name && b[index]?.[1] === message);
}

/**
 * Validates `values` against `schema` on submit and calls `onValid` with the schema's
 * OUTPUT (transforms applied), so coercions such as money-input normalization reach the
 * wire. The schema parse below is the submission authority — react-hook-form's own submit
 * verdict is not trusted because its handleSubmit discards path-less `root` resolver
 * errors, which would let a whole-object schema failure through as a "valid" submit.
 * A rejected submit is never silent: every failure renders in the role="alert" summary,
 * and while that summary is up the form revalidates on each change so it clears as the
 * user repairs the input. The resolver still feeds field errors to react-hook-form so
 * children can read them from context, and the form keeps `noValidate` because these
 * rendered errors replace native browser validation.
 */
export function ValidatedForm<T extends object>({ children, className, schema, values, onValid }: ValidatedFormProps<T>) {
  const form = useForm<FormValues>({
    resolver: standardSchemaResolver(schema as unknown as z.ZodType<FormValues, FormValues>),
    values: values as FormValues,
  });
  const [failures, setFailures] = useState<[string, string][]>([]);
  const submitted = useRef(false);
  // Preserves array identity when nothing changed so a consumer re-render cannot loop.
  const applyFailures = useCallback((next: [string, string][]) => {
    setFailures((current) => (sameFailures(current, next) ? current : next));
  }, []);
  const runValidation = useCallback((): T | undefined => {
    const parsed = schema.safeParse(values);
    if (parsed.success) {
      applyFailures([]);
      return parsed.data;
    }
    applyFailures(failuresFromIssues(parsed.error.issues));
    return undefined;
  }, [applyFailures, schema, values]);
  useEffect(() => {
    if (!submitted.current) return;
    runValidation();
    // Keep react-hook-form's own error state in sync for context consumers.
    void form.trigger();
  }, [form, runValidation]);
  const submitValidated = async () => {
    const validated = runValidation();
    if (validated !== undefined) await onValid(validated);
  };
  const submit = form.handleSubmit(submitValidated, () => void submitValidated());
  return (
    <Form {...form}>
      <form
        className={className}
        noValidate
        onSubmit={(event) => {
          submitted.current = true;
          void submit(event);
        }}
      >
        {failures.length > 0 && (
          <div role="alert" className="error-banner">
            <p>The form was not saved. Fix the following:</p>
            <ul>
              {failures.map(([name, message]) => (
                <li key={name}>{message}</li>
              ))}
            </ul>
          </div>
        )}
        {children}
      </form>
    </Form>
  );
}
