import { useEffect, useState } from 'react';
import { ErrorState, LoadingState } from '../components';
import { formatLocalDateTime } from '../dateTime';
import { getHealth, type HealthResponse } from '../api';

export function HealthPage() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    getHealth().then(setHealth).catch(setError);
  }, []);

  if (error) return <ErrorState message={error.message} />;
  if (!health) return <LoadingState label="Checking API..." />;
  return (
    <main>
      <h1>API status</h1>
      <p>{health.status}</p>
      <p>{formatLocalDateTime(health.timestampUtc)}</p>
    </main>
  );
}
