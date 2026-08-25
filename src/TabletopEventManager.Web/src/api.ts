const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: string } | null;
    throw new Error(body?.error ?? `API request failed with status ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export type HealthResponse = {
  status: string;
  timestampUtc: string;
};

export type Game = { id: number; code: string; displayName: string };
export type ConfigurationValue = { id: number; value: string; label: string; sortOrder: number };
export type ConfigurationOption = {
  id: number;
  key: string;
  label: string;
  dataType: string;
  uiControl: string;
  defaultValue: string | null;
  isRequired: boolean;
  sortOrder: number;
  values: ConfigurationValue[];
};
export type GameConfiguration = { gameId: number; options: ConfigurationOption[] };
export type EventSummary = {
  id: number;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  capacity: number;
  location: string | null;
  playType: string;
  tournamentFormat: string | null;
  registrationSlug: string;
  gameName: string;
  registrationCount: number;
};
export type CreateEventRequest = {
  name: string;
  gameId: number;
  startAtUtc: string;
  capacity: number;
  playType: string;
  tournamentFormat: string;
  configurationSelections: Record<string, string[]>;
};
export type EventConfigurationSelection = { key: string; label: string; values: string[] };
export type EventDetail = EventSummary & {
  gameCode: string;
  configurationSelections: EventConfigurationSelection[];
};
export type RegistrationResources = { registrationUrl: string; qrCodeDataUri: string };

export function getHealth(): Promise<HealthResponse> {
  return apiRequest<HealthResponse>('/health');
}

export function getGames(): Promise<Game[]> {
  return apiRequest<Game[]>('/api/games');
}

export function getGameConfiguration(gameId: number): Promise<GameConfiguration> {
  return apiRequest<GameConfiguration>(`/api/games/${gameId}/configuration`);
}

export function getEvents(startUtc: string, endUtc: string): Promise<EventSummary[]> {
  return apiRequest<EventSummary[]>(`/api/events?startUtc=${encodeURIComponent(startUtc)}&endUtc=${encodeURIComponent(endUtc)}`);
}

export function createEvent(request: CreateEventRequest): Promise<EventSummary> {
  return apiRequest<EventSummary>('/api/events', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export function getEventDetail(eventId: number): Promise<EventDetail> {
  return apiRequest<EventDetail>(`/api/events/${eventId}`);
}

export async function deleteEvent(eventId: number): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/events/${eventId}`, { method: 'DELETE' });
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: string } | null;
    throw new Error(body?.error ?? `API request failed with status ${response.status}`);
  }
}

export function getRegistrationResources(eventId: number): Promise<RegistrationResources> {
  return apiRequest<RegistrationResources>(`/api/events/${eventId}/registration-resources`);
}

export function getCalendarInviteUrl(eventId: number): string {
  return `${apiBaseUrl}/api/events/${eventId}/calendar-invite`;
}
