import { Dialog as BaseDialog } from '@base-ui-components/react/dialog';
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
      <BaseDialog.Backdrop className="fixed inset-0 bg-black/50" />
      <BaseDialog.Popup className={className ? `modal ${className}` : 'modal'} {...props} />
    </BaseDialog.Portal>
  );
}
