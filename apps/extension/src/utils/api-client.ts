// ============================================
// API Client for Backend Communication
// ============================================

import type { 
  CoverLetterRequest, 
  CoverLetterResponse, 
  CvParseResponse, 
  CustomizeCvResponse,
  MatchCvResponse,
  ApiError,
  CoverLetterFromTextRequest
} from '../types/index.js';
import { getUserId, getApiKey } from './storage.js';
import { withTelemetry, type TelemetryMiddlewareContext } from './extension-telemetry.js';

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

type ApiMethod = 'GET' | 'POST' | 'PUT' | 'DELETE';

type ApiRequestOptions = {
  method?: ApiMethod;
  body?: any;
  headers?: HeadersInit;
  includeUserId?: boolean;
  includeApiKey?: boolean;
  includeIdempotencyKey?: boolean;
  maxRetries?: number;
  responseType?: 'json' | 'blob';
};

type ApiRequestContext<TResponse> = {
  endpoint: string;
  url: string;
  method: ApiMethod;
  body?: any;
  headers: Headers;
  includeUserId: boolean;
  includeApiKey: boolean;
  includeIdempotencyKey: boolean;
  maxRetries: number;
  responseType: 'json' | 'blob';
  requestInit: RequestInit;
  response?: Response;
  parsedResponse?: TResponse;
  telemetryContext?: TelemetryMiddlewareContext;
};

type ApiMiddleware<TResponse> = (
  context: ApiRequestContext<TResponse>,
  next: () => Promise<void>
) => Promise<void>;

const authHeadersMiddleware: ApiMiddleware<any> = async (context, next) => {
  if (context.includeUserId) {
    const userId = await getUserId();
    context.headers.set('X-User-Id', userId);
  }

  if (context.includeApiKey) {
    const apiKey = await getApiKey();
    if (apiKey) {
      context.headers.set('X-Api-Key', apiKey);
    }
  }

  await next();
};

const idempotencyMiddleware: ApiMiddleware<any> = async (context, next) => {
  if (context.includeIdempotencyKey) {
    context.headers.set('X-Idempotency-Key', generateIdempotencyKey());
  }

  await next();
};

const requestBodyMiddleware: ApiMiddleware<any> = async (context, next) => {
  if (context.body && !(context.body instanceof FormData)) {
    context.headers.set('Content-Type', 'application/json');
  }

  context.requestInit = {
    method: context.method,
    headers: context.headers,
    body: context.body
      ? context.body instanceof FormData
        ? context.body
        : JSON.stringify(context.body)
      : undefined,
  };

  await next();
};

const retryFetchMiddleware: ApiMiddleware<any> = async (context, next) => {
  context.response = await fetchWithRetry(context.url, context.requestInit, context.maxRetries);
  await next();
};

const responseErrorMiddleware: ApiMiddleware<any> = async (context, next) => {
  if (!context.response) {
    throw new Error('Pipeline invariant violated: response is missing.');
  }

  if (!context.response.ok) {
    let error: ApiError;
    try {
      error = await context.response.json();
    } catch {
      const text = await context.response.text();
      error = { detail: text, title: 'Error', status: context.response.status, type: 'error' };
    }

    if (context.telemetryContext) {
      context.telemetryContext.failureLevel = context.response.status >= 500 ? 'error' : 'warning';
      context.telemetryContext.metadata.status = String(context.response.status);
    }

    throw new ApiClientError(context.response.status, error);
  }

  await next();
};

const responseParseMiddleware: ApiMiddleware<any> = async (context, next) => {
  if (!context.response) {
    throw new Error('Pipeline invariant violated: response is missing.');
  }

  context.parsedResponse = context.responseType === 'blob'
    ? await context.response.blob()
    : await context.response.json();

  if (context.telemetryContext) {
    context.telemetryContext.metadata.status = String(context.response.status);
  }

  await next();
};

const telemetryMiddleware: ApiMiddleware<any> = async (context, next) => {
  await withTelemetry(
    {
      eventType: 'api_request',
      source: 'api-client',
      baseMetadata: {
        endpoint: context.endpoint,
        method: context.method,
        operation: inferOperationFromEndpoint(context.endpoint, context.method)
      }
    },
    async telemetry => {
      context.telemetryContext = telemetry;
      await next();
    }
  );
};

function inferOperationFromEndpoint(endpoint: string, method: ApiMethod): string {
  if (method === 'POST' && endpoint === '/cover-letters/generate') return 'generate_cover_letter';
  if (method === 'POST' && endpoint === '/cover-letters/generate-from-text') return 'generate_cover_letter_from_text';
  if (method === 'POST' && endpoint === '/cv/customize') return 'customize_cv';
  if (method === 'POST' && endpoint === '/cv/parse') return 'parse_cv_file';
  if (method === 'POST' && endpoint === '/cv/parse-text') return 'parse_cv_text';
  if (method === 'POST' && endpoint === '/cv/match') return 'match_cv';
  if (method === 'POST' && endpoint === '/cv/compile') return 'compile_latex';
  if (method === 'GET' && endpoint === '/prompts/templates') return 'get_prompt_templates';
  if (method === 'POST' && endpoint === '/textarea-answers/generate') return 'generate_textarea_answer';
  if (method === 'POST' && endpoint === '/settings/groq-api-key') return 'save_groq_api_key';
  if (method === 'GET' && endpoint === '/settings/groq-api-key') return 'get_groq_api_key';
  if (method === 'DELETE' && endpoint === '/settings/groq-api-key') return 'delete_groq_api_key';
  if (endpoint.startsWith('/settings/prompts/')) {
    if (method === 'POST') return 'save_custom_prompt';
    if (method === 'GET') return 'get_custom_prompt';
    if (method === 'DELETE') return 'delete_custom_prompt';
  }

  return sanitizeOperationName(`${method.toLowerCase()}_${endpoint}`);
}

function sanitizeOperationName(raw: string): string {
  return raw
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 60) || 'unknown_operation';
}

async function runPipeline<TResponse>(
  context: ApiRequestContext<TResponse>,
  middlewares: ApiMiddleware<TResponse>[]
): Promise<void> {
  const invoke = async (index: number): Promise<void> => {
    const middleware = middlewares[index];
    if (!middleware) {
      return;
    }

    await middleware(context, async () => invoke(index + 1));
  };

  await invoke(0);
}

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
  options: ApiRequestOptions = {}
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

  const context: ApiRequestContext<T> = {
    endpoint,
    url: `${BASE_URL}${endpoint}`,
    method,
    body,
    headers: new Headers(customHeaders),
    includeUserId,
    includeApiKey,
    includeIdempotencyKey,
    maxRetries,
    responseType,
    requestInit: { method, headers: new Headers(customHeaders) }
  };

  const middlewares: ApiMiddleware<T>[] = [
    telemetryMiddleware,
    authHeadersMiddleware,
    idempotencyMiddleware,
    requestBodyMiddleware,
    retryFetchMiddleware,
    responseErrorMiddleware,
    responseParseMiddleware,
  ];

  await runPipeline(context, middlewares);

  return context.parsedResponse as T;
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
 * Parse CV from text
 */
export async function parseCvText(cvText: string): Promise<CvParseResponse> {
  return apiRequest<CvParseResponse>('/cv/parse-text', {
    method: 'POST',
    body: { cvText },
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
  textareaAnswer: string;
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
export async function saveCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis' | 'textarea-answer', prompt: string): Promise<void> {
  await apiRequest(`/settings/prompts/${promptType}`, {
    method: 'POST',
    body: { prompt },
  });
}

/**
 * Get saved custom prompt for a specific type
 */
export async function getCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis' | 'textarea-answer'): Promise<string | null> {
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
export async function deleteCustomPrompt(promptType: 'cv-customization' | 'cover-letter' | 'match-analysis' | 'textarea-answer'): Promise<void> {
  await apiRequest(`/settings/prompts/${promptType}`, {
    method: 'DELETE',
  });
}

/**
 * Generate answer to a textarea question using CV info
 */
export async function generateTextareaAnswer(
  cvId: string,
  fieldLabel: string,
  userQuestion: string,
  jobContext?: { jobTitle?: string; companyName?: string; jobDescription?: string }
): Promise<{ answer: string }> {
  return apiRequest<{ answer: string }>('/textarea-answers/generate', {
    method: 'POST',
    body: {
      cvId,
      fieldLabel,
      userQuestion,
      jobTitle: jobContext?.jobTitle,
      companyName: jobContext?.companyName,
      jobDescription: jobContext?.jobDescription,
    },
    includeIdempotencyKey: true,
  });
}
