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
  return apiRequest<MatchCvResponse>('/cv/match', {
    method: 'POST',
    body: { cvId, jobDescription },
    includeIdempotencyKey: true,
  });
}

/**
 * Generate idempotency key for POST requests
 */
function generateIdempotencyKey(): string {
  return crypto.randomUUID();
}

/**
 * Generic API request function that handles common headers and retry logic
 */
async function apiRequest<T = any>(
  endpoint: string,
  options: {
    method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
    body?: any;
    headers?: HeadersInit;
    includeUserId?: boolean;
    includeApiKey?: boolean;
    includeIdempotencyKey?: boolean;
    maxRetries?: number;
    responseType?: 'json' | 'blob';
  } = {}
): Promise<T> {
  const {
    method = 'GET',
    body,
    headers: customHeaders = {},
    includeUserId = true,
    includeApiKey = true,
    includeIdempotencyKey = false,
    maxRetries = 3,
    responseType = 'json',
  } = options;

  // Build headers
  const headers: HeadersInit = { ...customHeaders };
  
  if (includeUserId) {
    const userId = await getUserId();
    headers['X-User-Id'] = userId;
  }
  
  if (includeApiKey) {
    const apiKey = await getApiKey();
    if (apiKey) {
      headers['X-Api-Key'] = apiKey;
    }
  }
  
  if (includeIdempotencyKey) {
    headers['X-Idempotency-Key'] = generateIdempotencyKey();
  }

  // Add Content-Type for JSON body
  if (body && !(body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  // Build request options
  const requestOptions: RequestInit = {
    method,
    headers,
  };

  if (body) {
    requestOptions.body = body instanceof FormData ? body : JSON.stringify(body);
  }

  // Make request with retry logic
  const response = await fetchWithRetry(`${BASE_URL}${endpoint}`, requestOptions, maxRetries);

  // Handle non-OK responses
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

  // Return appropriate response type
  if (responseType === 'blob') {
    return (await response.blob()) as T;
  }
  
  return await response.json();
}

/**
 * Make HTTP request with retry logic (internal helper)
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
  const formData = new FormData();
  formData.append('file', file);
  
  return apiRequest<CvParseResponse>('/cv/parse', {
    method: 'POST',
    body: formData,
    includeIdempotencyKey: true,
  });
}

/**
 * Generate cover letter
 */
export async function generateCoverLetter(
  request: CoverLetterRequest
): Promise<CoverLetterResponse> {
  const { idempotencyKey, ...restOfRequest } = request;
  
  return apiRequest<CoverLetterResponse>('/cover-letters/generate', {
    method: 'POST',
    body: restOfRequest,
    includeIdempotencyKey: true,
  });
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
  return apiRequest<CustomizeCvResponse>('/cv/customize', {
    method: 'POST',
    body: { 
      cvId, 
      jobDescription,
      ...options
    },
    includeIdempotencyKey: true,
  });
}

/**
 * Compile raw LaTeX to PDF
 */
export async function compileLatex(latexSource: string): Promise<Blob> {
  return apiRequest<Blob>('/cv/compile', {
    method: 'POST',
    body: { latexSource },
    includeIdempotencyKey: true,
    responseType: 'blob',
  });
}

/**
 * Generate cover letter from direct text
 */
export async function generateCoverLetterFromText(
  request: CoverLetterFromTextRequest
): Promise<CoverLetterResponse> {
  const { idempotencyKey, ...restOfRequest } = request;
  
  return apiRequest<CoverLetterResponse>('/cover-letters/generate-from-text', {
    method: 'POST',
    body: restOfRequest,
    includeIdempotencyKey: true,
  });
}

import { CoverLetterFromTextRequest } from '../types/index.js';

/**
 * Save Groq API key to backend
 */
export async function saveGroqApiKey(apiKey: string): Promise<void> {
  await apiRequest('/settings/groq-api-key', {
    method: 'POST',
    body: { apiKey },
    includeApiKey: false, // Don't send API key when saving API key
  });
}

/**
 * Get saved Groq API key from backend
 */
export async function getGroqApiKey(): Promise<string | null> {
  try {
    const data = await apiRequest<{ hasKey: boolean; maskedKey?: string }>('/settings/groq-api-key', {
      method: 'GET',
      includeApiKey: false,
    });
    return data.hasKey ? (data.maskedKey || '••••••••••••••••') : null;
  } catch (error) {
    if (error instanceof ApiClientError && error.status === 401) {
      return null;
    }
    throw error;
  }
}

/**
 * Delete Groq API key from backend
 */
export async function deleteGroqApiKey(): Promise<void> {
  try {
    await apiRequest('/settings/groq-api-key', {
      method: 'DELETE',
      includeApiKey: false,
    });
  } catch (error) {
    // Ignore 404 errors
    if (error instanceof ApiClientError && error.status === 404) {
      return;
    }
    throw error;
  }
}

/**
 * Get all prompt templates
 */
export async function getPromptTemplates(): Promise<{
  cvCustomization: string;
  coverLetter: string;
  matchAnalysis: string;
}> {
  return apiRequest('/prompts/templates', {
    method: 'GET',
    includeUserId: false,
    includeApiKey: false,
  });
}

/**
 * Save custom prompt for a specific type
 */
export async function saveCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis', prompt: string): Promise<void> {
  await apiRequest(`/settings/prompts/${promptType}`, {
    method: 'POST',
    body: { prompt },
  });
}

/**
 * Get saved custom prompt for a specific type
 */
export async function getCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis'): Promise<string | null> {
  try {
    const data = await apiRequest<{ prompt: string }>(`/settings/prompts/${promptType}`, {
      method: 'GET',
    });
    return data.prompt;
  } catch (error) {
    if (error instanceof ApiClientError && error.status === 404) {
      return null; // No custom prompt saved
    }
    throw error;
  }
}

/**
 * Delete saved custom prompt for a specific type
 */
export async function deleteCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis'): Promise<void> {
  await apiRequest(`/settings/prompts/${promptType}`, {
    method: 'DELETE',
  });
}
