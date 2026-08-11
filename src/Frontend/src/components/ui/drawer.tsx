import { Drawer as DrawerPrimitive } from '@base-ui/react/drawer';
import { createContext, useContext, type ComponentProps, type ReactNode } from 'react';

type DrawerPosition = 'right' | 'left' | 'top' | 'bottom';

const DrawerPositionContext = createContext<DrawerPosition>('bottom');
const swipeDirection: Record<DrawerPosition, ComponentProps<typeof DrawerPrimitive.Root>['swipeDirection']> = {
  bottom: 'down',
  left: 'left',
  right: 'right',
  top: 'up',
};

type DrawerProps = ComponentProps<typeof DrawerPrimitive.Root> & { position?: DrawerPosition };
type DrawerPopupProps = ComponentProps<typeof DrawerPrimitive.Popup> & {
  position?: DrawerPosition;
  showBackdrop?: boolean;
};

/** coss UI's Base UI drawer, normalized to Tradebook's token-locked CSS surface. */
export function Drawer({ position = 'bottom', ...props }: DrawerProps) {
  return (
    <DrawerPositionContext.Provider value={position}>
      <DrawerPrimitive.Root swipeDirection={props.swipeDirection ?? swipeDirection[position]} {...props} />
    </DrawerPositionContext.Provider>
  );
}

export function DrawerPopup({ position: positionProp, showBackdrop = true, children, ...props }: DrawerPopupProps) {
  const contextPosition = useContext(DrawerPositionContext);
  const position = positionProp ?? contextPosition;
  return (
    <DrawerPrimitive.Portal>
      {showBackdrop && <DrawerPrimitive.Backdrop data-slot="drawer-backdrop" />}
      <DrawerPrimitive.Viewport data-position={position} data-slot="drawer-viewport">
        <DrawerPrimitive.Popup data-position={position} data-slot="drawer-popup" {...props}>
          {children}
        </DrawerPrimitive.Popup>
      </DrawerPrimitive.Viewport>
    </DrawerPrimitive.Portal>
  );
}

export function DrawerContent({ children, ...props }: ComponentProps<typeof DrawerPrimitive.Content> & { children?: ReactNode }) {
  return <DrawerPrimitive.Content data-slot="drawer-content" {...props}>{children}</DrawerPrimitive.Content>;
}

export function DrawerTitle(props: ComponentProps<typeof DrawerPrimitive.Title>) {
  return <DrawerPrimitive.Title data-slot="drawer-title" {...props} />;
}

export function DrawerDescription(props: ComponentProps<typeof DrawerPrimitive.Description>) {
  return <DrawerPrimitive.Description data-slot="drawer-description" {...props} />;
}
