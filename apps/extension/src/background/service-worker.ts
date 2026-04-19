// ============================================
// Background Service Worker
// ============================================

import { 
  customizeCv, 
  generateCoverLetter, 
  matchCv, 
  compileLatex,
  getPromptTemplates,
  saveCustomPrompt,
  getCustomPrompt,
  deleteCustomPrompt,
  generateTextareaAnswer
} from '../utils/api-client.js';
import { trackExtensionEvent, withTelemetry, type TelemetryMiddlewareOptions } from '../utils/extension-telemetry.js';
import type { ChromeMessage } from '../types/index.js';

/**
 * Handler Registry: Declarative mapping of message types to handlers with telemetry config.
 * Each handler is automatically wrapped with telemetry pipeline—no repetition needed.
 */
type HandlerDef = {
  type: ChromeMessage['type'];
  telemetryConfig?: Omit<TelemetryMiddlewareOptions, 'eventType' | 'source'>;
  handler: (payload?: any) => Promise<any>;
};

const handlerRegistry = new Map<ChromeMessage['type'], HandlerDef>();

function deriveMessageTypeFromHandlerName(handlerName: string): ChromeMessage['type'] {
  const normalized = handlerName
    .replace(/^handle/, '')
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2')
    .toUpperCase();

  return normalized as ChromeMessage['type'];
}

function registerHandler(
  handler: (payload?: any) => Promise<any>,
  telemetryConfig?: Omit<TelemetryMiddlewareOptions, 'eventType' | 'source'>
): (payload?: any) => Promise<any> {
  const type = deriveMessageTypeFromHandlerName(handler.name);
  handlerRegistry.set(type, { type, telemetryConfig, handler });
  return handler;
}

/**
 * Message listener with pipeline telemetry: each handler is automatically wrapped
 * based on its registry config. No repeated withTelemetry() boilerplate.
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  const handlerDef = handlerRegistry.get(message.type);
  
  if (!handlerDef) {
    return false;
  }

  // Execute with automatic telemetry wrapping
  void executeWithTelemetry(handlerDef, message.payload, sendResponse);
  
  return true;
});

/**
 * Unified telemetry pipeline: wraps execution with optional telemetry dispatch.
 * - If telemetryConfig provided: wraps call with withTelemetry
 * - Automatically derives eventType from message type
 * - Handles success/error responses uniformly
 */
async function executeWithTelemetry(
  handlerDef: HandlerDef,
  payload: any,
  sendResponse: (msg: any) => void
) {
  try {
    if (handlerDef.telemetryConfig) {
      // Derive event type from message type: MATCH_CV_DIRECT -> match_cv
      const eventType = handlerDef.type
        .replace(/_DIRECT$/, '')
        .toLowerCase();

      const result = await withTelemetry(
        {
          eventType,
          source: 'background',
          ...handlerDef.telemetryConfig
        },
        () => handlerDef.handler(payload)
      );

      sendResponse({ type: 'SUCCESS', payload: result });
    } else {
      // No telemetry config: direct execution
      const result = await handlerDef.handler(payload);
      sendResponse({ type: 'SUCCESS', payload: result });
    }
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Helper to get CV ID from storage
 */
async function getCvId(): Promise<string> {
   const data = await chrome.storage.local.get(['cvId']);
   if (!data.cvId) {
     throw new Error('No CV found. Please upload your CV in the extension first.');
   }
   return data.cvId;
}

/**
 * Logic for Tailoring CV (PDF)
 * Note: Telemetry automatically applied via pipeline (trackSuccess:false avoids
 * log duplication with api-client middleware).
 */
const handleCustomizeCvDirect = registerHandler(async function handleCustomizeCvDirect(jobData: any): Promise<any> {
  const cvId = await getCvId();
  const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;

  // Save job data to storage so popup can sync
  await chrome.storage.local.set({ 
    lastJobData: {
      jobTitle: jobData.jobTitle,
      companyName: jobData.companyName,
      jobDescription: jobData.jobDescription
    }
  });

  const customizationResult = await customizeCv(cvId, fullJobDesc, {
    selectedKeywords: jobData.selectedKeywords,
    customPromptTemplate: jobData.customPromptTemplate,
    promptMode: jobData.promptMode ?? 0
  });

  // Persist editor state for the popup
  await chrome.storage.local.set({
    editorState: {
      latex: customizationResult.latexSource,
      pdfBase64: customizationResult.pdfContent,
      updatedAt: Date.now()
    }
  });

  return customizationResult;
}, { trackSuccess: false, awaitDispatch: true });

/**
 * Logic for Generating Cover Letter (Text)
 * Note: Telemetry automatically applied via pipeline (trackSuccess:false avoids
 * log duplication with api-client middleware).
 */
const handleGenerateCoverLetterDirect = registerHandler(async function handleGenerateCoverLetterDirect(jobData: any): Promise<any> {
  const cvId = await getCvId();
  const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;

  // Save job data to storage so popup can sync
  await chrome.storage.local.set({ 
    lastJobData: {
      jobTitle: jobData.jobTitle,
      companyName: jobData.companyName,
      jobDescription: jobData.jobDescription
    }
  });

  const coverLetterResult = await generateCoverLetter({
    cvId,
    jobDescription: fullJobDesc,
    customPromptTemplate: jobData.customPromptTemplate,
    promptMode: jobData.promptMode ?? 0
  });

  // Persist cover letter for the popup
  await chrome.storage.local.set({ generatedCoverLetter: coverLetterResult.coverLetter });

  return coverLetterResult;
}, { trackSuccess: false, awaitDispatch: true });

/**
 * Logic for AI Match Analysis
 * Note: Telemetry automatically applied via pipeline (trackSuccess:false avoids
 * log duplication with api-client middleware).
 */
const handleMatchCvDirect = registerHandler(async function handleMatchCvDirect(jobData: any): Promise<any> {
  const cvId = await getCvId();
  const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;
  return await matchCv(cvId, fullJobDesc);
}, { trackSuccess: false, awaitDispatch: true });

/**
 * Logic for Compiling LaTeX
 * Note: Telemetry automatically applied via pipeline (trackSuccess:false avoids
 * log duplication with api-client middleware).
 */
const handleCompileLatexDirect = registerHandler(async function handleCompileLatexDirect(payload: any): Promise<any> {
  // compileLatex returns a Blob, we need to convert it to base64 for messaging
  const blob = await compileLatex(payload.latexSource);
  
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => {
      resolve({ pdfContent: (reader.result as string).split(',')[1] });
    };
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });
}, { trackSuccess: false, awaitDispatch: true });

/**
 * Logic for Opening Overleaf
 */
const handleOpenOverleafDirect = registerHandler(async function handleOpenOverleafDirect(payload: any): Promise<void> {
  const { latexSource } = payload;
  const encodedSnip = encodeURIComponent(latexSource);
  
  // We use a data URI to create a page that auto-submits the form
  const formPage = `
    <!DOCTYPE html>
    <html>
      <head><title>Opening Overleaf...</title></head>
      <body>
        <form id="overleafForm" action="https://www.overleaf.com/docs" method="POST">
          <input type="hidden" name="encoded_snip" value="${encodedSnip}">
          <input type="hidden" name="snip_name" value="Resume.tex">
        </form>
        <script>
          document.getElementById('overleafForm').submit();
        </script>
      </body>
    </html>
  `;
  
  const dataUrl = 'data:text/html;charset=utf-8,' + encodeURIComponent(formPage);
  chrome.tabs.create({ url: dataUrl });
}, { trackSuccess: true });

/**
 * Get prompt templates
 */
const handleViewPromptsDirect = registerHandler(async function handleViewPromptsDirect(): Promise<any> {
  return await getPromptTemplates();
});

/**
 * Save custom prompt
 */
const handleSaveCustomPrompt = registerHandler(async function handleSaveCustomPrompt(payload: any): Promise<void> {
  await saveCustomPrompt(payload.promptType, payload.prompt);
});

/**
 * Get custom prompt
 */
const handleGetCustomPrompt = registerHandler(async function handleGetCustomPrompt(payload: any): Promise<any> {
  const prompt = await getCustomPrompt(payload.promptType);
  return { prompt };
});

/**
 * Delete custom prompt
 */
const handleDeleteCustomPrompt = registerHandler(async function handleDeleteCustomPrompt(payload: any): Promise<void> {
  await deleteCustomPrompt(payload.promptType);
});

/**
 * Generate answer to textarea question using CV
 * Note: Telemetry automatically applied via pipeline (trackSuccess:false avoids
 * log duplication with api-client middleware).
 */
const handleGenerateTextareaAnswer = registerHandler(async function handleGenerateTextareaAnswer(payload: any): Promise<any> {
  const cvId = await getCvId();
  
  // Get job context if available
  const storageData = await chrome.storage.local.get(['lastJobData']);
  const jobContext = storageData.lastJobData || {};
  
  return await generateTextareaAnswer(
    cvId,
    payload.fieldLabel,
    payload.userQuestion,
    payload.includeJobContext ? jobContext : undefined
  );
}, { trackSuccess: false, awaitDispatch: true });

const handleTrackExtensionEvent = registerHandler(async function handleTrackExtensionEvent(payload: any): Promise<void> {
  return trackExtensionEvent({
    eventType: payload?.eventType || 'content_event',
    source: payload?.source || 'content',
    success: payload?.success ?? true,
    level: payload?.level,
    message: payload?.message,
    durationMs: payload?.durationMs,
    metadata: payload?.metadata
  });
});

chrome.runtime.onInstalled.addListener(() => {
  console.log('[Service Worker] Extension Active');
  void trackExtensionEvent({
    eventType: 'extension_installed',
    source: 'background',
    success: true,
    message: 'Extension installed/updated'
  });
});
