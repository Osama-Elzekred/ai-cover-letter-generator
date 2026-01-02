// ============================================
// Background Service Worker
// ============================================


import { 
  customizeCv, 
  generateCoverLetter, 
  matchCv, 
  compileLatex 
} from '../utils/api-client.js';
import type { ChromeMessage } from '../types/index.js';

/**
 * Handle messages from popup and content scripts
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  console.log('[Service Worker] Received message:', message.type);

  // Return true to indicate we will send a response asynchronously
  
  if (message.type === 'CUSTOMIZE_CV_DIRECT') {
    handleCustomizeCv(message.payload, sendResponse);
    return true; 
  }
  
  if (message.type === 'GENERATE_COVER_LETTER_DIRECT') {
    handleGenerateCoverLetter(message.payload, sendResponse);
    return true;
  }

  if (message.type === 'MATCH_CV_DIRECT') {
    handleMatchCv(message.payload, sendResponse);
    return true;
  }

  if (message.type === 'COMPILE_LATEX_DIRECT') {
    handleCompileLatex(message.payload, sendResponse);
    return true;
  }

  if (message.type === 'OPEN_OVERLEAF_DIRECT') {
    handleOpenOverleaf(message.payload);
    sendResponse({ type: 'SUCCESS' });
    return true;
  }
  
  return false;
});

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
 */
async function handleCustomizeCv(jobData: any, sendResponse: (msg: any) => void) {
  try {
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

    const result = await customizeCv(cvId, fullJobDesc, {
      selectedKeywords: jobData.selectedKeywords,
      customPromptTemplate: jobData.customPromptTemplate,
      promptMode: jobData.promptMode || 0
    });
    
    // Persist editor state for the popup
    await chrome.storage.local.set({
      editorState: {
        latex: result.latexSource,
        pdfBase64: result.pdfContent,
        updatedAt: Date.now()
      }
    });

    sendResponse({ type: 'SUCCESS', payload: result });
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for Generating Cover Letter (Text)
 */
async function handleGenerateCoverLetter(jobData: any, sendResponse: (msg: any) => void) {
  try {
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
    
    const result = await generateCoverLetter({
      cvId,
      jobDescription: fullJobDesc,
      customPromptTemplate: jobData.customPromptTemplate,
      promptMode: jobData.promptMode || 1
    });
    
    // Persist cover letter for the popup
    await chrome.storage.local.set({ generatedCoverLetter: result.coverLetter });

    sendResponse({ type: 'SUCCESS', payload: result });
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for AI Match Analysis
 */
async function handleMatchCv(jobData: any, sendResponse: (msg: any) => void) {
  try {
    const cvId = await getCvId();
    const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;
    
    const result = await matchCv(cvId, fullJobDesc);
    sendResponse({ type: 'SUCCESS', payload: result });
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for Compiling LaTeX
 */
async function handleCompileLatex(payload: any, sendResponse: (msg: any) => void) {
  try {
    // compileLatex returns a Blob, we need to convert it to base64 for messaging
    const blob = await compileLatex(payload.latexSource);
    
    const reader = new FileReader();
    reader.onloadend = () => {
      sendResponse({ 
        type: 'SUCCESS', 
        payload: { pdfContent: (reader.result as string).split(',')[1] } 
      });
    };
    reader.readAsDataURL(blob);
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for Opening Overleaf
 */
function handleOpenOverleaf(payload: any) {
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
}

chrome.runtime.onInstalled.addListener(() => console.log('[Service Worker] Extension Active'));
