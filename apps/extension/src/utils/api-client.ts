// ============================================
// API Client for Backend Communication
// ============================================

import type { 
  CoverLetterRequest, 
  CoverLetterResponse, 
  CvParseResponse, 
  CustomizeCvResponse,
  MatchCvResponse,
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
    public apiError?: ApiError | any,
    message?: string
  ) {
    let errorMsg = message || apiError?.detail || apiError?.title;

    // If no detail provided but we have validation errors, try to extract them
    if (!errorMsg && apiError?.errors) {
      if (Array.isArray(apiError.errors)) {
        errorMsg = apiError.errors[0];
      } else if (typeof apiError.errors === 'object') {
        const firstKey = Object.keys(apiError.errors)[0];
        if (firstKey) {
          const firstVal = apiError.errors[firstKey];
          errorMsg = Array.isArray(firstVal) ? firstVal[0] : firstVal;
        }
      }
    }

    super(errorMsg || 'Unknown API Error');
    this.name = 'ApiClientError';
  }
}

// ... existing code ...

/**
 * Match CV against job description
 */
export async function matchCv(
  cvId: string,
  jobDescription: string
): Promise<MatchCvResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'X-Idempotency-Key': generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cv/match`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ cvId, jobDescription }),
  });
  
  if (!response.ok) {
    let error: ApiError;
    try {
      error = await response.json();
    } catch {
      const text = await response.text();
      error = { detail: text, title: 'Error', status: response.status, type: 'error' };
    }
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
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
    'X-Idempotency-Key': generateIdempotencyKey(),
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
  
  const { idempotencyKey, ...restOfRequest } = request;
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'X-Idempotency-Key': idempotencyKey || generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cover-letters/generate`, {
    method: 'POST',
    headers,
    body: JSON.stringify(restOfRequest),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
}

/**
 * Customize CV based on job description
 */
export async function customizeCv(
  cvId: string,
  jobDescription: string,
  options?: {
    selectedKeywords?: string[];
    customPromptTemplate?: string;
    promptMode?: number;
  }
): Promise<CustomizeCvResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'X-Idempotency-Key': generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cv/customize`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ 
      cvId, 
      jobDescription,
      ...options
    }),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.json();
}

/**
 * Compile raw LaTeX to PDF
 */
export async function compileLatex(latexSource: string): Promise<Blob> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'X-Idempotency-Key': generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cv/compile`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ latexSource }),
  });
  
  if (!response.ok) {
    const error: ApiError = await response.json();
    throw new ApiClientError(response.status, error);
  }
  
  return await response.blob();
}

/**
 * Generate cover letter from direct text
 */
export async function generateCoverLetterFromText(
  request: CoverLetterFromTextRequest
): Promise<CoverLetterResponse> {
  const userId = await getUserId();
  const apiKey = await getApiKey();
  
  const { idempotencyKey, ...restOfRequest } = request;
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-User-Id': userId,
    'X-Idempotency-Key': idempotencyKey || generateIdempotencyKey(),
  };
  
  if (apiKey) {
    headers['X-Api-Key'] = apiKey;
  }
  
  const response = await fetchWithRetry(`${BASE_URL}/cover-letters/generate-from-text`, {
    method: 'POST',
    headers,
    body: JSON.stringify(restOfRequest),
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
