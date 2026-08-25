export function formatLocalDateTime(timestampUtc: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(timestampUtc));
}

export function toUtcIso(localDateTime: string): string {
  return new Date(localDateTime).toISOString();
}

export function monthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

export function localDateKey(timestampUtc: string): string {
  const date = new Date(timestampUtc);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

export function formatLocalTime(timestampUtc: string): string {
  return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(timestampUtc));
}
