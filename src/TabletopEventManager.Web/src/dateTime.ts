export function formatLocalDateTime(timestampUtc: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(timestampUtc));
}

export function toUtcIso(localDateTime: string): string {
  return new Date(localDateTime).toISOString();
}
