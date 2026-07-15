import type {
  Configuration,
  CreateConfigurationRequest,
  UpdateConfigurationRequest,
} from './types';

const API_KEY_HEADER = 'X-Api-Key';
const CONFIGURATIONS_PATH = '/api/configurations';

/**
 * Raised when the Admin API returns a non-success response. Carries the HTTP status and a
 * human-readable message, so callers (and ultimately the user) get an actionable error.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5147').replace(/\/$/, '');
const apiKey = import.meta.env.VITE_ADMIN_API_KEY ?? '';

/**
 * Thin HTTP client for the Admin API. Every request carries the X-Api-Key header, baked in at
 * build time from VITE_ADMIN_API_KEY (same pattern as baseUrl — Vite has no runtime env).
 */
export const configurationsApi = {
  list(): Promise<Configuration[]> {
    return request<Configuration[]>('GET', CONFIGURATIONS_PATH);
  },

  create(payload: CreateConfigurationRequest): Promise<Configuration> {
    return request<Configuration>('POST', CONFIGURATIONS_PATH, payload);
  },

  update(id: string, payload: UpdateConfigurationRequest): Promise<Configuration> {
    return request<Configuration>('PUT', `${CONFIGURATIONS_PATH}/${id}`, payload);
  },
};

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: buildHeaders(body !== undefined),
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new ApiError(response.status, await extractErrorMessage(response));
  }

  return (await response.json()) as T;
}

function buildHeaders(hasBody: boolean): HeadersInit {
  const headers: Record<string, string> = { [API_KEY_HEADER]: apiKey };
  if (hasBody) {
    headers['Content-Type'] = 'application/json';
  }
  return headers;
}

/**
 * Turns a failed response into a readable message. The API surfaces validation failures as a
 * ProblemDetails document with a field->messages 'errors' map (CFG-5.2); we flatten those so
 * the user sees exactly which field was rejected and why.
 */
async function extractErrorMessage(response: Response): Promise<string> {
  if (response.status === 401) {
    return 'Yetkisiz. API anahtarını kontrol edin.';
  }

  const problem = await readProblemDetails(response);
  if (!problem) {
    return `İstek başarısız oldu (durum: ${response.status}).`;
  }

  if (problem.errors) {
    const fieldMessages = Object.values(problem.errors).flat();
    if (fieldMessages.length > 0) {
      return fieldMessages.join(' ');
    }
  }

  return problem.title ?? `İstek başarısız oldu (durum: ${response.status}).`;
}

interface ProblemDetails {
  title?: string;
  errors?: Record<string, string[]>;
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}
