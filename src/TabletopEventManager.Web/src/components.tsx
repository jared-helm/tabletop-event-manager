import type { ReactNode } from 'react';

export function LoadingState({ label = 'Loading...' }: { label?: string }) {
  return <p role="status">{label}</p>;
}

export function ErrorState({ message = 'Something went wrong.' }: { message?: string }) {
  return <p role="alert">{message}</p>;
}

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  return (
    <div className="modal-backdrop" role="presentation" onClick={onClose}>
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" onClick={(event) => event.stopPropagation()}>
        <header>
          <h2 id="modal-title">{title}</h2>
          <button type="button" aria-label="Close" onClick={onClose}>X</button>
        </header>
        {children}
      </section>
    </div>
  );
}

export function Tabs({ tabs, activeTab, onChange }: { tabs: string[]; activeTab: string; onChange: (tab: string) => void }) {
  return (
    <div role="tablist" aria-label="Event sections">
      {tabs.map((tab) => (
        <button key={tab} type="button" role="tab" aria-selected={tab === activeTab} onClick={() => onChange(tab)}>
          {tab}
        </button>
      ))}
    </div>
  );
}
