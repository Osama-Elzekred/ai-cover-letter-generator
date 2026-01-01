// ============================================
// Background Service Worker
// ============================================

interface ChromeMessage<T = any> {
  type: string;
  payload?: T;
  error?: string;
}

const BASE_URL = 'http://localhost:5012/api/v1';

/**
 * Handle messages from popup and content scripts
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  console.log('[Service Worker] Received message:', message.type);

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
 * Common logic to get auth details from storage
 */
async function getAuthDetails() {
  const data = await chrome.storage.local.get(['cvId', 'userId', 'apiKey']);
  if (!data.cvId) throw new Error('No CV found. Please upload your CV in the extension first.');
  return {
    cvId: data.cvId,
    userId: data.userId || 'default-user',
    apiKey: data.apiKey
  };
}

/**
 * Logic for Tailoring CV (PDF)
 */
async function handleCustomizeCv(jobData: any, sendResponse: (msg: any) => void) {
  try {
    const { cvId, userId, apiKey } = await getAuthDetails();
    const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;
    
    // Save job data to storage so popup can sync
    await chrome.storage.local.set({ 
      lastJobData: {
        jobTitle: jobData.jobTitle,
        companyName: jobData.companyName,
        jobDescription: jobData.jobDescription
      }
    });

    const headers: any = {
      'Content-Type': 'application/json',
      'X-User-Id': userId,
      'X-Idempotency-Key': crypto.randomUUID(),
    };
    if (apiKey) headers['X-Api-Key'] = apiKey;

    const response = await fetch(`${BASE_URL}/cv/customize`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ 
        cvId, 
        jobDescription: fullJobDesc,
        selectedKeywords: jobData.selectedKeywords,
        customPromptTemplate: jobData.customPromptTemplate,
        promptMode: jobData.promptMode || 0 
      })
    });

    if (!response.ok) throw new Error('API failed with status ' + response.status);

    const result = await response.json();
    
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
    const { cvId, userId, apiKey } = await getAuthDetails();
    const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;
    
    // Save job data to storage so popup can sync
    await chrome.storage.local.set({ 
      lastJobData: {
        jobTitle: jobData.jobTitle,
        companyName: jobData.companyName,
        jobDescription: jobData.jobDescription
      }
    });

    const headers: any = {
      'Content-Type': 'application/json',
      'X-User-Id': userId,
      'X-Idempotency-Key': crypto.randomUUID(),
    };
    if (apiKey) headers['X-Api-Key'] = apiKey;

    const response = await fetch(`${BASE_URL}/cover-letters/generate`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ 
        cvId, 
        jobDescription: fullJobDesc, 
        customPromptTemplate: jobData.customPromptTemplate,
        promptMode: jobData.promptMode || 1 
      }) 
    });

    if (!response.ok) throw new Error('API failed with status ' + response.status);

    const data = await response.json();
    
    // Persist cover letter for the popup
    await chrome.storage.local.set({ generatedCoverLetter: data.coverLetter });

    sendResponse({ type: 'SUCCESS', payload: data });
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for AI Match Analysis
 */
async function handleMatchCv(jobData: any, sendResponse: (msg: any) => void) {
  try {
    const { cvId, userId, apiKey } = await getAuthDetails();
    const fullJobDesc = `Job Title: ${jobData.jobTitle}\nCompany: ${jobData.companyName}\n\nJob Description:\n${jobData.jobDescription}`;
    
    const headers: any = {
      'Content-Type': 'application/json',
      'X-User-Id': userId,
      'X-Idempotency-Key': crypto.randomUUID(),
    };
    if (apiKey) headers['X-Api-Key'] = apiKey;

    const response = await fetch(`${BASE_URL}/cv/match`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ cvId, jobDescription: fullJobDesc })
    });

    if (!response.ok) throw new Error('API failed with status ' + response.status);

    const data = await response.json();
    sendResponse({ type: 'SUCCESS', payload: data });
  } catch (error: any) {
    sendResponse({ type: 'ERROR', error: error.message });
  }
}

/**
 * Logic for Compiling LaTeX
 */
async function handleCompileLatex(payload: any, sendResponse: (msg: any) => void) {
  try {
    const { userId, apiKey } = await getAuthDetails();
    
    const headers: any = {
      'Content-Type': 'application/json',
      'X-User-Id': userId,
      'X-Idempotency-Key': crypto.randomUUID(),
    };
    if (apiKey) headers['X-Api-Key'] = apiKey;

    const response = await fetch(`${BASE_URL}/cv/compile`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ latexSource: payload.latexSource })
    });

    if (!response.ok) throw new Error('Compilation failed. Check LaTeX syntax.');

    const blob = await response.blob();
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
