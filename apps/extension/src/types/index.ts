// ============================================
// Shared TypeScript Type Definitions
// ============================================

// Job data extracted from LinkedIn or other job sites
export interface JobData {
  jobTitle: string;
  companyName: string;
  jobDescription: string;
}

// API Request/Response Types
export interface CoverLetterRequest {
  cvId: string;
  jobTitle: string;
  companyName: string;
  jobDescription: string;
}

export interface CoverLetterResponse {
  coverLetter: string;
  generatedAt: string;
  model?: string;
  promptTokens?: number;
  completionTokens?: number;
}

export interface CvParseResponse {
  cvId: string;
  fileName: string;
  uploadedAt: string;
}

export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

// Chrome Storage Schema
export interface StorageData {
  userId?: string;
  cvId?: string;
  cvFileName?: string;
  apiKey?: string;
  lastJobData?: JobData;
}

// Chrome Runtime Messages
export type MessageType = 
  | 'EXTRACT_JOB_DATA'
  | 'JOB_DATA_EXTRACTED'
  | 'GENERATE_COVER_LETTER'
  | 'UPLOAD_CV'
  | 'ERROR';

export interface ChromeMessage<T = any> {
  type: MessageType;
  payload?: T;
  error?: string;
}

// API Client Types
export interface ApiClientConfig {
  baseUrl: string;
  userId: string;
  apiKey?: string;
}

export interface RetryConfig {
  maxRetries: number;
  baseDelay: number;
}
