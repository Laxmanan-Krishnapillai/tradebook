import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import type { ReactNode } from 'react';
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

export function ValidatedForm<T extends object>({ children, className, schema, values, onValid }: ValidatedFormProps<T>) {
  const form = useForm<Record<string, unknown>>({
    resolver: standardSchemaResolver(schema as unknown as z.ZodType<Record<string, unknown>, Record<string, unknown>>),
    values: values as Record<string, unknown>,
  });
  const submit = form.handleSubmit(async (validated) => onValid(validated as T));
  return <Form {...form}><form className={className} noValidate onSubmit={(event) => void submit(event)}>{children}</form></Form>;
}
