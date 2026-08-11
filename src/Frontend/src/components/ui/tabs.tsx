import { Tabs as TabsPrimitive } from '@base-ui/react/tabs';
import type { ComponentProps } from 'react';

function classes(...values: Array<string | undefined>) {
  return values.filter(Boolean).join(' ');
}

type TabsVariant = 'default' | 'underline';
type RootProps = Omit<ComponentProps<typeof TabsPrimitive.Root>, 'className'> & { className?: string };
type ListProps = Omit<ComponentProps<typeof TabsPrimitive.List>, 'className'> & { className?: string; variant?: TabsVariant };
type TabProps = Omit<ComponentProps<typeof TabsPrimitive.Tab>, 'className'> & { className?: string };
type PanelProps = Omit<ComponentProps<typeof TabsPrimitive.Panel>, 'className'> & { className?: string };

export function Tabs({ className, ...props }: RootProps) {
  return <TabsPrimitive.Root className={classes('ui-tabs', className)} data-slot="tabs" {...props} />;
}

export function TabsList({ className, variant = 'default', children, ...props }: ListProps) {
  return (
    <TabsPrimitive.List className={classes('ui-tabs-list', className)} data-slot="tabs-list" data-variant={variant} {...props}>
      {children}
      <TabsPrimitive.Indicator className="ui-tabs-indicator" data-slot="tab-indicator" />
    </TabsPrimitive.List>
  );
}

export function TabsTab({ className, ...props }: TabProps) {
  return <TabsPrimitive.Tab className={classes('ui-tabs-tab', className)} data-slot="tabs-tab" {...props} />;
}

export function TabsPanel({ className, ...props }: PanelProps) {
  return <TabsPrimitive.Panel className={classes('ui-tabs-panel', className)} data-slot="tabs-content" {...props} />;
}
