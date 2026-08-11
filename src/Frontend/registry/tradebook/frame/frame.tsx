import type { ComponentProps } from 'react';

function classes(...values: Array<string | undefined>) {
  return values.filter(Boolean).join(' ');
}

/** coss UI's Frame composition, normalized to Tradebook's token-locked CSS surface. */
export function Frame({ className, ...props }: ComponentProps<'section'>) {
  return <section className={classes('ui-frame', className)} data-slot="frame" {...props} />;
}

export function FramePanel({ className, ...props }: ComponentProps<'div'>) {
  return <div className={classes('ui-frame-panel', className)} data-slot="frame-panel" {...props} />;
}

export function FrameHeader({ className, ...props }: ComponentProps<'header'>) {
  return <header className={classes('ui-frame-header', className)} data-slot="frame-panel-header" {...props} />;
}

export function FrameTitle({ className, ...props }: ComponentProps<'div'>) {
  return <div className={classes('ui-frame-title', className)} data-slot="frame-panel-title" {...props} />;
}

export function FrameDescription({ className, ...props }: ComponentProps<'div'>) {
  return <div className={classes('ui-frame-description', className)} data-slot="frame-panel-description" {...props} />;
}

export function FrameFooter({ className, ...props }: ComponentProps<'footer'>) {
  return <footer className={classes('ui-frame-footer', className)} data-slot="frame-panel-footer" {...props} />;
}
