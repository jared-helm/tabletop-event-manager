import { useEffect, useMemo, useState } from 'react';
import { ErrorState, LoadingState } from '../components';
import { formatDateTimeLocal, formatLocalTime, localDateKey, toUtcIso } from '../dateTime';
import { getEvents, type EventSummary } from '../api';
import { CreateEventModal } from './CreateEventModal';
import { EventModal } from './EventModal';

const WEEKDAY_LABELS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export function CalendarPage() {
  const [isCreateOpen, setCreateOpen] = useState(false);
  const [selectedEventId, setSelectedEventId] = useState<number | null>(null);
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
        <div className="calendar-toolbar">
          <button type="button" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1))}>Previous</button>
          <strong>{month.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })}</strong>
          <button type="button" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1))}>Next</button>
          <button type="button" onClick={() => setMonth(new Date(new Date().getFullYear(), new Date().getMonth(), 1))}>Today</button>
        </div>
        <div className="calendar-weekdays">
          {WEEKDAY_LABELS.map((day) => <strong key={day}>{day.slice(0, 3)}</strong>)}
        </div>
        {loading && <LoadingState label="Loading events..." />}
        {error && <ErrorState message={error.message} />}
        <div className="calendar-grid">
          {calendarDays.map((date) => {
            const key = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
            return (
              <div className={`calendar-day ${date.getMonth() !== month.getMonth() ? 'outside-month' : ''}`} key={date.toISOString()}>
                <time dateTime={date.toISOString()}>{date.getDate()}</time>
                {(eventsByDay.get(key) ?? []).map((event) => (
                  <button
                    className="calendar-event"
                    title={`${formatLocalTime(event.startAtUtc)} - ${event.name}`}
                    type="button"
                    key={event.id}
                    onClick={() => setSelectedEventId(event.id)}
                  >
                    <span>{formatLocalTime(event.startAtUtc)}</span> {event.name}
                  </button>
                ))}
              </div>
            );
          })}
        </div>
      </section>
      {isCreateOpen && (
        <CreateEventModal
          onClose={() => setCreateOpen(false)}
          onCreated={() => { setCreateOpen(false); loadEvents(); }}
        />
      )}
      {selectedEventId !== null && (
        <EventModal
          eventId={selectedEventId}
          onClose={() => setSelectedEventId(null)}
          onDeleted={() => { setSelectedEventId(null); loadEvents(); }}
        />
      )}
    </main>
  );
}
