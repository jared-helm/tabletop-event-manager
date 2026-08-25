import { useEffect, useState } from 'react';
import { Route, Routes } from 'react-router-dom';
import { ErrorState, LoadingState, Modal, Tabs } from './components';
import { formatLocalDateTime } from './dateTime';
import { getHealth, type HealthResponse } from './api';
import './styles.css';

function CalendarPage() {
  const [isCreateOpen, setCreateOpen] = useState(false);

  return (
    <main>
      <header className="page-header">
        <div>
          <p className="eyebrow">Organizer workspace</p>
          <h1>Event calendar</h1>
        </div>
        <button type="button" onClick={() => setCreateOpen(true)}>Create event</button>
      </header>
      <section className="calendar-placeholder" aria-label="Calendar placeholder">
        <p>Month calendar coming next.</p>
      </section>
      {isCreateOpen && <Modal title="Create event" onClose={() => setCreateOpen(false)}><p>The event form will be added in the event-creation phase.</p></Modal>}
    </main>
  );
}

function RegistrationPage() {
  return <main><p className="eyebrow">Player registration</p><h1>Registration page</h1><p>Registration will be available when event sharing is implemented.</p></main>;
}

function HealthPage() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    getHealth().then(setHealth).catch(setError);
  }, []);

  if (error) return <ErrorState message={error.message} />;
  if (!health) return <LoadingState label="Checking API..." />;
  return <main><h1>API status</h1><p>{health.status}</p><p>{formatLocalDateTime(health.timestampUtc)}</p></main>;
}

export default function App() {
  return (
    <>
      <Routes>
        <Route path="/" element={<CalendarPage />} />
        <Route path="/registration/:slug" element={<RegistrationPage />} />
        <Route path="/health" element={<HealthPage />} />
      </Routes>
    </>
  );
}

export { CalendarPage, RegistrationPage };
