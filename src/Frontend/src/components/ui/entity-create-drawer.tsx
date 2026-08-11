import type { ReactNode } from 'react';
import { X } from 'lucide-react';
import { Button } from './button';
import { Drawer, DrawerContent, DrawerDescription, DrawerPopup, DrawerTitle } from './drawer';

interface EntityCreateDrawerProps {
  children: ReactNode;
  description: string;
  onOpenChange: (open: boolean) => void;
  open: boolean;
  title: string;
}

/** Product composition over coss UI's animated Base UI drawer primitive. */
export function EntityCreateDrawer({ children, description, onOpenChange, open, title }: EntityCreateDrawerProps) {
  return (
    <Drawer modal onOpenChange={onOpenChange} open={open} position="right">
      <DrawerPopup data-variant="entity-create-drawer">
        <DrawerContent data-slot="entity-create-drawer">
          <header data-slot="entity-create-drawer-header">
            <div>
              <span data-slot="entity-create-drawer-kicker">New record</span>
              <DrawerTitle>{title}</DrawerTitle>
              <DrawerDescription>{description}</DrawerDescription>
            </div>
            <Button aria-label="Close create drawer" intent="ghost" size="icon" type="button" onClick={() => onOpenChange(false)}>
              <X aria-hidden="true" size={15} />
            </Button>
          </header>
          <div data-slot="entity-create-drawer-body">{children}</div>
        </DrawerContent>
      </DrawerPopup>
    </Drawer>
  );
}
