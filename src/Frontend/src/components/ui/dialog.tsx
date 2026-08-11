import { Dialog as BaseDialog } from '@base-ui/react/dialog';
import type { ComponentProps } from 'react';

export const Dialog = BaseDialog.Root;
export const DialogTrigger = BaseDialog.Trigger;
export const DialogClose = BaseDialog.Close;
export const DialogTitle = BaseDialog.Title;
export const DialogDescription = BaseDialog.Description;

type DialogContentProps = Omit<ComponentProps<typeof BaseDialog.Popup>, 'className'> & { className?: string };

export function DialogContent({ className, ...props }: DialogContentProps) {
  return (
    <BaseDialog.Portal>
      <BaseDialog.Backdrop data-slot="dialog-backdrop" />
      <BaseDialog.Popup data-slot="dialog-content" className={className} {...props} />
    </BaseDialog.Portal>
  );
}
