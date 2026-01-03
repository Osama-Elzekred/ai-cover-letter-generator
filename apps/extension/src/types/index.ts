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
  jobDescription: string;
  customPromptTemplate?: string | null;
  promptMode?: number;
  idempotencyKey?: string | null;
}

export interface CoverLetterFromTextRequest {
  cvText: string;
  jobDescription: string;
  customPromptTemplate?: string | null;
  promptMode?: number;
  idempotencyKey?: string | null;
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

export interface CustomizeCvResponse {
  pdfContent: string; // Base64
  latexSource: string;
  fileName: string;
  model: string;
}

export interface CompileLatexRequest {
  latexSource: string;
}

export interface MatchCvResponse {
  matchScore: number;
  matchingKeywords: string[];
  missingKeywords: string[];
  analysisSummary: string;
}

export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]> | string[];
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
  | 'ERROR'
  | 'CUSTOMIZE_CV_DIRECT'
  | 'GENERATE_COVER_LETTER_DIRECT'
  | 'MATCH_CV_DIRECT'
  | 'COMPILE_LATEX_DIRECT'
  | 'OPEN_OVERLEAF_DIRECT'
  | 'VIEW_PROMPTS_DIRECT'
  | 'SAVE_CUSTOM_PROMPT'
  | 'GET_CUSTOM_PROMPT'
  | 'DELETE_CUSTOM_PROMPT';

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
