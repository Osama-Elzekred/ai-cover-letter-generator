// ============================================
// API Client for Backend Communication
// ============================================

import type { 
  CoverLetterRequest, 
  CoverLetterResponse, 
  CvParseResponse, 
  ApiError 
} from '../types/index.js';
import { getUserId, getApiKey } from './storage.js';

const BASE_URL = 'http://localhost:5012/api/v1';

/**
 * Custom error class for API errors
 */
export class ApiClientError extends Error {
  constructor(
    public status: number,
    public apiError: ApiError,
    message?: string
  ) {
    super(message || apiError.detail || apiError.title);
    this.name = 'ApiClientError';
  }
}

/**
 * Generate idempotency key for POST requests
 */
function generateIdempotencyKey(): string {
  return crypto.randomUUID();
}

/**
 * Make HTTP request with retry logic
 */
async function fetchWithRetry(
  url: string,
  options: RequestInit,
  maxRetries = 3
): Promise<Response> {
  let lastError: Error | null = null;
  
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      const response = await fetch(url, options);
      
      // Don't retry 4xx errors (client errors)
      if (response.status >= 400 && response.status < 500) {
        return response;
      }
      
      // Retry 5xx errors (server errors)
      if (response.status >= 500 && attempt < maxRetries) {
        const delay = Math.pow(2, attempt) * 1000; // Exponential backoff
        await new Promise(resolve => setTimeout(resolve, delay));
        continue;
      }
      
      return response;
    } catch (error) {
      lastError = error as Error;
      
      // Retry network errors
      if (attempt < maxRetries) {
        const delay = Math.pow(2, attempt) * 1000;
        await new Promise(resolve => setTimeout(resolve, delay));
        continue;
      }
    }
  }
  
  throw lastError || new Error('Request failed after retries');
}

/**
 * Parse CV file
 */
export async function parseCv(file: File): Promise<CvParseResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const formData = new FormData();
  formData.append('file', file);
  
  const headers: HeadersInit = {
    'X-User-Id': userId,
    'Idempotency-Key': generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cv/parse`, {
    method: 'POST',
    headers,
    body: formData,
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
}

/**
 * Generate cover letter
 */
export async function generateCoverLetter(
  request: CoverLetterRequest
): Promise<CoverLetterResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'Idempotency-Key': generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cover-letters/generate`, {
    method: 'POST',
    headers,
    body: JSON.stringify(request),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
}

/**
 * Generate cover letter from direct text
 */
export async function generateCoverLetterFromText(
  request: CoverLetterFromTextRequest
): Promise<CoverLetterResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'Idempotency-Key': request.idempotencyKey || generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cover-letters/generate-from-text`, {
    method: 'POST',
    headers,
    body: JSON.stringify(request),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
}

import { CoverLetterFromTextRequest } from '../types/index.js';
export async function saveGroqApiKey(apiKey: string): Promise<void> {
  const userId = await getUserId();
  
  const response = await fetchWithRetry(`${BASE_URL}/settings/groq-api-key`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-User-Id': userId,
    },
    body: JSON.stringify({ apiKey }),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
}

/**
 * Get saved Groq API key from backend
 */
export async function getGroqApiKey(): Promise<string | null> {
  const userId = await getUserId();
  
  const response = await fetchWithRetry(`${BASE_URL}/settings/groq-api-key`, {
    method: 'GET',
    headers: {
      'X-User-Id': userId,
    },
  });
  
  if (response.status === 401) {
    return null;
  }
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  const data = await response.json();
  return data.hasKey ? (data.maskedKey || '••••••••••••••••') : null;
}

/**
 * Delete Groq API key from backend
 */
export async function deleteGroqApiKey(): Promise<void> {
  const userId = await getUserId();
  
  const response = await fetchWithRetry(`${BASE_URL}/settings/groq-api-key`, {
    method: 'DELETE',
    headers: {
      'X-User-Id': userId,
    },
  });
  
  if (!response.ok && response.status !== 404) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
}
