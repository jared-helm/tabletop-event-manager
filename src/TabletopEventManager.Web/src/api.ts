const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  });

  if (!response.ok) {
    throw new Error(`API request failed with status ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export type HealthResponse = {
  status: string;
  timestampUtc: string;
};

export function getHealth(): Promise<HealthResponse> {
  return apiRequest<HealthResponse>('/health');
}
