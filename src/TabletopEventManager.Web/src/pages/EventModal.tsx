import { useEffect, useState } from 'react';
import { ErrorState, LoadingState, Modal, Tabs } from '../components';
import { formatLocalDateTime } from '../dateTime';
import { deleteEvent, getCalendarInviteUrl, getEventDetail, getRegistrationResources, type EventDetail, type RegistrationResources } from '../api';

const PLAY_TYPE_LABELS: Record<string, string> = {
  CASUAL: 'Casual/Friendly',
  TOURNAMENT: 'Tournament',
};

const TOURNAMENT_FORMAT_LABELS: Record<string, string> = {
  SWISS_TOP_CUT: 'Swiss + Top Cut',
  DOUBLE_ELIMINATION: 'Double Elimination',
};

const TABS = ['Event Details', 'Players', 'Registration Resources'];

export function EventModal({ eventId, onClose, onDeleted }: { eventId: number; onClose: () => void; onDeleted: () => void }) {
  const [activeTab, setActiveTab] = useState(TABS[0]);
  const [event, setEvent] = useState<EventDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [isDeleting, setDeleting] = useState(false);
  const [resources, setResources] = useState<RegistrationResources | null>(null);
  const [resourcesError, setResourcesError] = useState<string | null>(null);
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    getEventDetail(eventId)
      .then(setEvent)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
    getRegistrationResources(eventId)
      .then(setResources)
      .catch((reason: Error) => setResourcesError(reason.message));
  }, [eventId]);

  const copyRegistrationUrl = async () => {
    if (!resources) return;
    try {
      await navigator.clipboard.writeText(resources.registrationUrl);
      setCopyFeedback('Copied!');
    } catch {
      setCopyFeedback('Could not copy the link.');
    }
  };

  const requestDelete = async () => {
    if (!window.confirm('Delete this event? This cannot be undone.')) return;
    setDeleting(true);
    setError(null);
    try {
      await deleteEvent(eventId);
      onDeleted();
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Modal title={event?.name ?? 'Event details'} onClose={onClose}>
      <Tabs tabs={TABS} activeTab={activeTab} onChange={setActiveTab} />
      {loading && <LoadingState label="Loading event..." />}
      {error && <ErrorState message={error} />}
      {!loading && event && activeTab === 'Event Details' && (
        <div className="event-details">
          <dl>
            <dt>Game</dt>
            <dd>{event.gameName}</dd>
            <dt>Start</dt>
            <dd>{formatLocalDateTime(event.startAtUtc)}</dd>
            <dt>End</dt>
            <dd>{formatLocalDateTime(event.endAtUtc)}</dd>
            <dt>Capacity</dt>
            <dd>{event.registrationCount} / {event.capacity} registered</dd>
            <dt>Play type</dt>
            <dd>{PLAY_TYPE_LABELS[event.playType] ?? event.playType}</dd>
            {event.tournamentFormat && (
              <>
                <dt>Tournament format</dt>
                <dd>{TOURNAMENT_FORMAT_LABELS[event.tournamentFormat] ?? event.tournamentFormat}</dd>
              </>
            )}
            {event.configurationSelections.map((selection) => (
              <div key={selection.key}>
                <dt>{selection.label}</dt>
                <dd>{selection.values.join(', ')}</dd>
              </div>
            ))}
          </dl>
          <button type="button" className="delete-event" onClick={requestDelete} disabled={isDeleting}>
            {isDeleting ? 'Deleting...' : 'Delete event'}
          </button>
        </div>
      )}
      {!loading && event && activeTab === 'Players' && (
        <p>Player registrations will be shown here once registration is implemented.</p>
      )}
      {!loading && event && activeTab === 'Registration Resources' && (
        <div className="registration-resources">
          {resourcesError && <ErrorState message={resourcesError} />}
          {!resourcesError && !resources && <LoadingState label="Loading registration resources..." />}
          {resources && (
            <>
              <p>
                <a href={resources.registrationUrl} target="_blank" rel="noreferrer">{resources.registrationUrl}</a>
              </p>
              <button type="button" onClick={copyRegistrationUrl}>Copy link</button>
              {copyFeedback && <span role="status" className="copy-feedback"> {copyFeedback}</span>}
              <p>
                <a href={getCalendarInviteUrl(eventId)}>Download calendar invite (.ics)</a>
              </p>
              <img src={resources.qrCodeDataUri} alt="QR code for the registration link" className="registration-qr" />
            </>
          )}
        </div>
      )}
    </Modal>
  );
}
