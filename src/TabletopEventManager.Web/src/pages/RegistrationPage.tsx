import { useEffect, useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { ErrorState, LoadingState } from '../components';
import { formatLocalDateTime } from '../dateTime';
import { getRegistrationContext, registerPlayer, type RegistrationConfirmation, type RegistrationPageContext } from '../api';

export function RegistrationPage() {
  const { slug = '' } = useParams();
  const [context, setContext] = useState<RegistrationPageContext | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState({ firstName: '', lastName: '', playerTag: '' });
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);
  const [confirmation, setConfirmation] = useState<RegistrationConfirmation | null>(null);

  useEffect(() => {
    getRegistrationContext(slug)
      .then(setContext)
      .catch((reason: Error) => setLoadError(reason.message))
      .finally(() => setLoading(false));
  }, [slug]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitError(null);
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setSubmitError('First name and last name are required.');
      return;
    }

    setSubmitting(true);
    try {
      const result = await registerPlayer(slug, form);
      setConfirmation(result);
    } catch (reason) {
      setSubmitError((reason as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <main className="registration-page"><LoadingState label="Loading event..." /></main>;
  if (loadError || !context) return <main className="registration-page"><ErrorState message={loadError ?? 'This event is no longer available.'} /></main>;

  if (confirmation) {
    return (
      <main className="registration-page">
        <div className="registration-card registration-confirmation">
          <p className="eyebrow">Registration confirmed</p>
          <h1>You're registered, {confirmation.firstName}!</h1>
          <p className="registration-event-name">{confirmation.eventName}</p>
          <p>{confirmation.gameName}</p>
          <p>{formatLocalDateTime(confirmation.startAtUtc)} - {formatLocalDateTime(confirmation.endAtUtc)}</p>
        </div>
      </main>
    );
  }

  const isFull = context.registrationCount >= context.capacity;

  return (
    <main className="registration-page">
      <div className="registration-card">
        <p className="eyebrow">Player registration</p>
        <h1>{context.eventName}</h1>
        <dl className="registration-summary">
          <dt>Game</dt>
          <dd>{context.gameName}</dd>
          <dt>When</dt>
          <dd>{formatLocalDateTime(context.startAtUtc)} - {formatLocalDateTime(context.endAtUtc)}</dd>
          {context.location && (
            <>
              <dt>Location</dt>
              <dd>{context.location}</dd>
            </>
          )}
          <dt>Capacity</dt>
          <dd>{context.registrationCount} / {context.capacity} registered</dd>
        </dl>

        {context.isClosed && <ErrorState message="Registration has closed for this event." />}
        {!context.isClosed && isFull && <ErrorState message="This event is full." />}
        {!context.isClosed && !isFull && (
          <form className="event-form registration-form" onSubmit={submit}>
            <label>
              First name
              <input value={form.firstName} onChange={(event) => setForm({ ...form, firstName: event.target.value })} maxLength={60} required />
            </label>
            <label>
              Last name
              <input value={form.lastName} onChange={(event) => setForm({ ...form, lastName: event.target.value })} maxLength={60} required />
            </label>
            <label>
              Player tag (optional)
              <input value={form.playerTag} onChange={(event) => setForm({ ...form, playerTag: event.target.value })} maxLength={60} />
            </label>
            {submitError && <ErrorState message={submitError} />}
            <button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Registering...' : 'Register'}</button>
          </form>
        )}
      </div>
    </main>
  );
}
