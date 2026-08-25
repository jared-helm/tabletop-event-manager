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
