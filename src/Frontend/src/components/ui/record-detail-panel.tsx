import type { ReactNode } from 'react';
import { Clock3, Copy, X } from 'lucide-react';
import { Button } from './button';
import { Drawer, DrawerContent, DrawerDescription, DrawerPopup, DrawerTitle } from './drawer';
import { Frame, FrameDescription, FrameHeader, FramePanel, FrameTitle } from './frame';
import { Tabs, TabsList, TabsPanel, TabsTab } from './tabs';

export interface RecordDetailPanelProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  eyebrow: string;
  title: string;
  description: string;
  recordId: string;
  version: number;
  dirty?: boolean;
  properties: ReactNode;
  context?: ReactNode;
  activity?: ReactNode;
  actions: ReactNode;
}

export function RecordDetailPanel({
  open,
  onOpenChange,
  eyebrow,
  title,
  description,
  recordId,
  version,
  dirty = false,
  properties,
  context,
  activity,
  actions,
}: RecordDetailPanelProps) {
  return (
    <Drawer
      modal
      onOpenChange={onOpenChange}
      open={open}
      position="right"
    >
      <DrawerPopup data-variant="record-detail-panel">
        <DrawerContent data-slot="record-workspace">
        <header data-slot="record-workspace-header">
          <div data-slot="record-workspace-heading">
            <div data-slot="record-workspace-kicker">
              <span>{eyebrow}</span>
              <span aria-hidden="true">/</span>
              <span>v{version}</span>
              {dirty && <span data-slot="record-dirty-state">Unsaved</span>}
            </div>
            <DrawerTitle>{title}</DrawerTitle>
            <DrawerDescription>{description}</DrawerDescription>
          </div>
          <Button aria-label="Close record" intent="ghost" size="sm" type="button" onClick={() => onOpenChange(false)}>
            <X aria-hidden="true" size={15} />
          </Button>
        </header>

        <Tabs defaultValue="overview" data-slot="record-workspace-tabs">
          <TabsList variant="underline" aria-label="Record sections">
            <TabsTab value="overview">Overview</TabsTab>
            <TabsTab value="activity">Activity</TabsTab>
          </TabsList>

          <TabsPanel value="overview">
            <div data-slot="record-workspace-grid">
              <Frame>
                <FrameHeader>
                  <FrameTitle>Properties</FrameTitle>
                  <FrameDescription>Edit the operational fields for this record.</FrameDescription>
                </FrameHeader>
                <FramePanel data-slot="record-properties">{properties}</FramePanel>
              </Frame>

              <aside data-slot="record-context-rail">
                {context}
                <Frame>
                  <FrameHeader>
                    <FrameTitle>Record details</FrameTitle>
                    <FrameDescription>Stable identifiers and concurrency state.</FrameDescription>
                  </FrameHeader>
                  <FramePanel>
                    <dl data-slot="record-metadata">
                      <div>
                        <dt>Record ID</dt>
                        <dd title={recordId}>{recordId}</dd>
                        <Button aria-label="Copy record ID" intent="ghost" size="sm" type="button" onClick={() => void navigator.clipboard.writeText(recordId)}>
                          <Copy aria-hidden="true" size={13} />
                        </Button>
                      </div>
                      <div>
                        <dt>Version</dt>
                        <dd>{version}</dd>
                      </div>
                    </dl>
                  </FramePanel>
                </Frame>
              </aside>
            </div>
          </TabsPanel>

          <TabsPanel value="activity" data-section="activity">
            <Frame>
              <FrameHeader>
                <FrameTitle>Activity</FrameTitle>
                <FrameDescription>Record history and changes in this session.</FrameDescription>
              </FrameHeader>
              <FramePanel>
                {activity ?? (
                  <ol data-slot="record-activity-list">
                    <li>
                      <Clock3 aria-hidden="true" size={15} />
                      <div>
                        <strong>Current revision</strong>
                        <span>Version {version} is loaded and ready to edit.</span>
                      </div>
                    </li>
                  </ol>
                )}
              </FramePanel>
            </Frame>
          </TabsPanel>
        </Tabs>

        <footer data-slot="record-workspace-footer">
          <span>{dirty ? 'Review and save your changes.' : 'No unsaved changes.'}</span>
          <div role="toolbar" aria-label="Record actions">{actions}</div>
        </footer>
        </DrawerContent>
      </DrawerPopup>
    </Drawer>
  );
}
