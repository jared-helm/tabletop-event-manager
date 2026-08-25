import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Route, Routes } from 'react-router-dom';
import { ErrorState, LoadingState, Modal, Tabs } from './components';
import { formatDateTimeLocal, formatLocalDateTime, formatLocalTime, localDateKey, toUtcIso } from './dateTime';
import { createEvent, getEvents, getGameConfiguration, getGames, getHealth, type ConfigurationOption, type EventSummary, type Game, type HealthResponse } from './api';
import './styles.css';

function CalendarPage() {
  const [isCreateOpen, setCreateOpen] = useState(false);
  const [month, setMonth] = useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  const [events, setEvents] = useState<EventSummary[]>([]);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(true);

  // Grid always spans full Sunday-to-Saturday weeks, so the visible range often
  // includes days from the adjacent month.
  const calendarDays = useMemo(() => {
    const firstDay = new Date(month.getFullYear(), month.getMonth(), 1);
    const start = new Date(firstDay);
    start.setDate(firstDay.getDate() - firstDay.getDay());
    return Array.from({ length: 42 }, (_, index) => {
      const date = new Date(start);
      date.setDate(start.getDate() + index);
      return date;
    });
  }, [month]);

  const loadEvents = () => {
    setLoading(true);
    const rangeStart = calendarDays[0];
    const rangeEnd = new Date(calendarDays[calendarDays.length - 1]);
    rangeEnd.setDate(rangeEnd.getDate() + 1);
    getEvents(toUtcIso(formatDateTimeLocal(rangeStart)), toUtcIso(formatDateTimeLocal(rangeEnd)))
      .then(setEvents)
      .catch(setError)
      .finally(() => setLoading(false));
  };
  useEffect(loadEvents, [calendarDays]);

  const eventsByDay = new Map<string, EventSummary[]>();
  events.forEach((event) => {
    const key = localDateKey(event.startAtUtc);
    eventsByDay.set(key, [...(eventsByDay.get(key) ?? []), event]);
  });

  return (
    <main>
      <header className="page-header">
        <div>
          <p className="eyebrow">Organizer workspace</p>
          <h1>Event calendar</h1>
        </div>
        <button type="button" onClick={() => setCreateOpen(true)}>Create event</button>
      </header>
      <section className="calendar" aria-label="Month calendar">
        <div className="calendar-toolbar"><button type="button" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))}>Previous</button><strong>{month.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })}</strong><button type="button" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))}>Next</button><button type="button" onClick={() => setMonth(new Date(new Date().getFullYear(), new Date().getMonth(), 1))}>Today</button></div>
        <div className="calendar-weekdays">{['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].map((day) => <strong key={day}>{day.slice(0, 3)}</strong>)}</div>
        {loading && <LoadingState label="Loading events..." />}
        {error && <ErrorState message={error.message} />}
        <div className="calendar-grid">
          {calendarDays.map((date) => <div className={`calendar-day ${date.getMonth() !== month.getMonth() ? 'outside-month' : ''}`} key={date.toISOString()}><time dateTime={date.toISOString()}>{date.getDate()}</time>{(eventsByDay.get(`${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`) ?? []).map((event) => <button className="calendar-event" title={`${formatLocalTime(event.startAtUtc)} - ${event.name}`} type="button" key={event.id}><span>{formatLocalTime(event.startAtUtc)}</span> {event.name}</button>)}</div>)}
        </div>
      </section>
      {isCreateOpen && <CreateEventModal onClose={() => setCreateOpen(false)} onCreated={() => { setCreateOpen(false); loadEvents(); }} />}
    </main>
  );
}

function CreateEventModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [games, setGames] = useState<Game[]>([]);
  const [gameId, setGameId] = useState<number>(0);
  const [configuration, setConfiguration] = useState<ConfigurationOption[]>([]);
  const [form, setForm] = useState({ name: '', startDate: '', startTime: '', capacity: '2', playType: 'CASUAL', tournamentFormat: '' });
  const [format, setFormat] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isDirty, setDirty] = useState(false);
  const timeOptions = Array.from({ length: 25 }, (_, index) => {
    const minutes = (8 * 60) + (index * 30);
    return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
  });
  const requestClose = () => {
    if (isDirty && !window.confirm('You have unsaved changes. Are you sure you want to close?')) return;
    onClose();
  };
  useEffect(() => { getGames().then((items) => { setGames(items); if (items[0]) setGameId(items[0].id); }).catch((reason: Error) => setError(reason.message)); }, []);
  useEffect(() => { if (gameId) getGameConfiguration(gameId).then((result) => setConfiguration(result.options)).catch((reason: Error) => setError(reason.message)); }, [gameId]);
  const formatOption = configuration.find((option) => option.key === 'event_format');
  const submit = async (event: FormEvent) => { event.preventDefault(); setError(null); if (!form.name.trim() || !form.startDate || !form.startTime || Number(form.capacity) < 0 || Number(form.capacity) > 30 || !format) { setError('Complete the required fields and use a capacity from 0 to 30.'); return; } try { await createEvent({ name: form.name, gameId, startAtUtc: toUtcIso(`${form.startDate}T${form.startTime}`), capacity: Number(form.capacity), playType: form.playType, tournamentFormat: form.tournamentFormat, configurationSelections: { event_format: [format] } }); onCreated(); } catch (reason) { setError((reason as Error).message); } };
  return <Modal title="Create event" onClose={requestClose}><form className="event-form" onSubmit={submit}><label>Event name<input value={form.name} onChange={(event) => { setDirty(true); setForm({ ...form, name: event.target.value }); }} maxLength={120} required /></label><label>Game<select value={gameId} onChange={(event) => { setDirty(true); setGameId(Number(event.target.value)); }} >{games.map((game) => <option value={game.id} key={game.id}>{game.displayName}</option>)}</select></label><label>Event date<input type="date" value={form.startDate} onChange={(event) => { setDirty(true); setForm({ ...form, startDate: event.target.value }); }} required /></label><label>Start time<select value={form.startTime} onChange={(event) => { setDirty(true); setForm({ ...form, startTime: event.target.value }); }} required><option value="">Choose a time</option>{timeOptions.map((time) => <option value={time} key={time}>{new Date(`2000-01-01T${time}`).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })}</option>)}</select></label><label>Capacity<input type="number" min="0" max="30" value={form.capacity} onChange={(event) => { setDirty(true); setForm({ ...form, capacity: event.target.value }); }} required /></label><label>Play type<select value={form.playType} onChange={(event) => { setDirty(true); setForm({ ...form, playType: event.target.value, tournamentFormat: event.target.value === 'CASUAL' ? '' : form.tournamentFormat }); }}><option value="CASUAL">Casual/Friendly</option><option value="TOURNAMENT">Tournament</option></select></label>{formatOption && <label>{formatOption.label}<select value={format} onChange={(event) => { setDirty(true); setFormat(event.target.value); }} required><option value="">Choose a format</option>{formatOption.values.map((value) => <option value={value.value} key={value.id}>{value.label}</option>)}</select></label>}{form.playType === 'TOURNAMENT' && <label>Tournament format<select value={form.tournamentFormat} onChange={(event) => { setDirty(true); setForm({ ...form, tournamentFormat: event.target.value }); }} required><option value="">Choose a format</option><option value="SWISS_TOP_CUT">Swiss + Top Cut</option><option value="DOUBLE_ELIMINATION">Double Elimination</option></select></label>}{error && <ErrorState message={error} />}<button type="submit">Create event</button></form></Modal>;
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
