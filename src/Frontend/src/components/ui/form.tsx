import type { FieldValues, FormProviderProps } from 'react-hook-form';
import { FormProvider } from 'react-hook-form';

export function Form<T extends FieldValues>(props: FormProviderProps<T>) {
  return <FormProvider {...props} />;
}
