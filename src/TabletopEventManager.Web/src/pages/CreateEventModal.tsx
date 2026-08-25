import { useEffect, useState, type FormEvent } from 'react';
import { ErrorState, Modal } from '../components';
import { toUtcIso } from '../dateTime';
import { createEvent, getGameConfiguration, getGames, type ConfigurationOption, type Game } from '../api';

const TIME_OPTIONS = Array.from({ length: 25 }, (_, index) => {
  const minutes = 8 * 60 + index * 30;
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
});

export function CreateEventModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [games, setGames] = useState<Game[]>([]);
  const [gameId, setGameId] = useState<number>(0);
  const [configuration, setConfiguration] = useState<ConfigurationOption[]>([]);
  const [form, setForm] = useState({ name: '', startDate: '', startTime: '', capacity: '2', durationMinutes: '', playType: 'CASUAL', tournamentFormat: '' });
  const [format, setFormat] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isDirty, setDirty] = useState(false);

  const requestClose = () => {
    if (isDirty && !window.confirm('You have unsaved changes. Are you sure you want to close?')) return;
    onClose();
  };

  useEffect(() => {
    getGames()
      .then((items) => {
        setGames(items);
        if (items[0]) setGameId(items[0].id);
      })
      .catch((reason: Error) => setError(reason.message));
  }, []);

  useEffect(() => {
    if (gameId) {
      getGameConfiguration(gameId)
        .then((result) => {
          setConfiguration(result.options);
          setFormat('');
          setForm((current) => ({ ...current, durationMinutes: '' }));
        })
        .catch((reason: Error) => setError(reason.message));
    }
  }, [gameId]);

  const formatOption = configuration.find((option) => option.key === 'event_format');
  const durationOption = configuration.find((option) => option.key === 'default_duration_minutes');

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    const durationMinutes = form.durationMinutes === '' ? undefined : Number(form.durationMinutes);
    if (!form.name.trim() || !form.startDate || !form.startTime || Number(form.capacity) < 0 || Number(form.capacity) > 30 || !format
      || (durationMinutes !== undefined && (!Number.isInteger(durationMinutes) || durationMinutes <= 0))) {
      setError('Complete the required fields, use a capacity from 0 to 30, and enter a positive whole-number duration when overriding the default.');
      return;
    }
    try {
      await createEvent({
        name: form.name,
        gameId,
        startAtUtc: toUtcIso(`${form.startDate}T${form.startTime}`),
        capacity: Number(form.capacity),
        durationMinutes,
        playType: form.playType,
        tournamentFormat: form.tournamentFormat,
        configurationSelections: { event_format: [format] },
      });
      onCreated();
    } catch (reason) {
      setError((reason as Error).message);
    }
  };

  return (
    <Modal title="Create event" onClose={requestClose}>
      <form className="event-form" onSubmit={submit}>
        <label>
          Event name
          <input value={form.name} onChange={(event) => { setDirty(true); setForm({ ...form, name: event.target.value }); }} maxLength={120} required />
        </label>
        <label>
          Game
          <select value={gameId} onChange={(event) => { setDirty(true); setGameId(Number(event.target.value)); }}>
            {games.map((game) => <option value={game.id} key={game.id}>{game.displayName}</option>)}
          </select>
        </label>
        <label>
          Event date
          <input type="date" value={form.startDate} onChange={(event) => { setDirty(true); setForm({ ...form, startDate: event.target.value }); }} required />
        </label>
        <label>
          Start time
          <select value={form.startTime} onChange={(event) => { setDirty(true); setForm({ ...form, startTime: event.target.value }); }} required>
            <option value="">Choose a time</option>
            {TIME_OPTIONS.map((time) => (
              <option value={time} key={time}>{new Date(`2000-01-01T${time}`).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })}</option>
            ))}
          </select>
        </label>
        <label>
          Capacity
          <input type="number" min="0" max="30" value={form.capacity} onChange={(event) => { setDirty(true); setForm({ ...form, capacity: event.target.value }); }} required />
        </label>
        <label>
          Duration (minutes)
          <input
            type="number"
            min="1"
            step="1"
            value={form.durationMinutes}
            placeholder={durationOption?.defaultValue ? `Default: ${durationOption.defaultValue}` : 'Use game default'}
            onChange={(event) => { setDirty(true); setForm({ ...form, durationMinutes: event.target.value }); }}
          />
        </label>
        <label>
          Play type
          <select
            value={form.playType}
            onChange={(event) => { setDirty(true); setForm({ ...form, playType: event.target.value, tournamentFormat: event.target.value === 'CASUAL' ? '' : form.tournamentFormat }); }}
          >
            <option value="CASUAL">Casual/Friendly</option>
            <option value="TOURNAMENT">Tournament</option>
          </select>
        </label>
        {formatOption && (
          <label>
            {formatOption.label}
            <select value={format} onChange={(event) => { setDirty(true); setFormat(event.target.value); }} required>
              <option value="">Choose a format</option>
              {formatOption.values.map((value) => <option value={value.value} key={value.id}>{value.label}</option>)}
            </select>
          </label>
        )}
        {form.playType === 'TOURNAMENT' && (
          <label>
            Tournament format
            <select value={form.tournamentFormat} onChange={(event) => { setDirty(true); setForm({ ...form, tournamentFormat: event.target.value }); }} required>
              <option value="">Choose a format</option>
              <option value="SWISS_TOP_CUT">Swiss + Top Cut</option>
              <option value="DOUBLE_ELIMINATION">Double Elimination</option>
            </select>
          </label>
        )}
        {error && <ErrorState message={error} />}
        <button type="submit">Create event</button>
      </form>
    </Modal>
  );
}
