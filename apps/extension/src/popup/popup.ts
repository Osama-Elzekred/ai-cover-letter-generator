// ============================================
// Popup UI Logic
// ============================================

import type { JobData, ChromeMessage, CoverLetterRequest } from '../types/index.js';
import * as storage from '../utils/storage.js';
import * as api from '../utils/api-client.js';

// ============================================
// DOM Elements
// ============================================

const cvUploadArea = document.getElementById('cvUploadArea') as HTMLDivElement;
const cvFileInput = document.getElementById('cvFileInput') as HTMLInputElement;
const uploadPlaceholder = document.getElementById('uploadPlaceholder') as HTMLDivElement;
const cvInfo = document.getElementById('cvInfo') as HTMLDivElement;
const cvFileName = document.getElementById('cvFileName') as HTMLSpanElement;
const deleteCvBtn = document.getElementById('deleteCvBtn') as HTMLButtonElement;
const customizeCvBtn = document.getElementById('customizeCvBtn') as HTMLButtonElement;

const extractJobBtn = document.getElementById('extractJobBtn') as HTMLButtonElement;
const jobTitleInput = document.getElementById('jobTitle') as HTMLInputElement;
const companyNameInput = document.getElementById('companyName') as HTMLInputElement;
const jobDescriptionInput = document.getElementById('jobDescription') as HTMLTextAreaElement;

const generateBtn = document.getElementById('generateBtn') as HTMLButtonElement;

const resultSection = document.getElementById('resultSection') as HTMLElement;
const resultContent = document.getElementById('resultContent') as HTMLDivElement;
const copyBtn = document.getElementById('copyBtn') as HTMLButtonElement;
const downloadBtn = document.getElementById('downloadBtn') as HTMLButtonElement;

const apiKeyInput = document.getElementById('apiKeyInput') as HTMLInputElement;
const saveApiKeyBtn = document.getElementById('saveApiKeyBtn') as HTMLButtonElement;
const deleteApiKeyBtn = document.getElementById('deleteApiKeyBtn') as HTMLButtonElement;
const apiKeyStatus = document.getElementById('apiKeyStatus') as HTMLDivElement;

const loadingOverlay = document.getElementById('loadingOverlay') as HTMLDivElement;
const loadingText = document.getElementById('loadingText') as HTMLParagraphElement;
const errorToast = document.getElementById('errorToast') as HTMLDivElement;
const errorMessage = document.getElementById('errorMessage') as HTMLSpanElement;
const successToast = document.getElementById('successToast') as HTMLDivElement;
const successMessage = document.getElementById('successMessage') as HTMLSpanElement;

const cvEditorSection = document.getElementById('cvEditorSection') as HTMLElement;
const latexSourceTextarea = document.getElementById('latexSource') as HTMLTextAreaElement;
const highlighting = document.getElementById('highlighting') as HTMLElement;
const highlightingContent = document.getElementById('highlighting-content') as HTMLElement;
const recompileBtn = document.getElementById('recompileBtn') as HTMLButtonElement;
const downloadTexBtn = document.getElementById('downloadTexBtn') as HTMLButtonElement;
const downloadPdfBtn = document.getElementById('downloadPdfBtn') as HTMLButtonElement;
const viewPdfBtn = document.getElementById('viewPdfBtn') as HTMLButtonElement;
const overleafBtn = document.getElementById('overleafBtn') as HTMLButtonElement;
const btnViewSource = document.getElementById('btnViewSource') as HTMLButtonElement;
const btnViewPreview = document.getElementById('btnViewPreview') as HTMLButtonElement;
const sourceEditor = document.getElementById('sourceEditor') as HTMLDivElement;
const pdfMessage = document.getElementById('pdfMessage') as HTMLDivElement;

// Tabs
const tabUpload = document.getElementById('tabUpload') as HTMLButtonElement;
const tabText = document.getElementById('tabText') as HTMLButtonElement;
const sectionUpload = document.getElementById('sectionUpload') as HTMLElement;
const sectionText = document.getElementById('sectionText') as HTMLElement;

// Text Input
const cvTextInput = document.getElementById('cvText') as HTMLTextAreaElement;

// Advanced Options
const customPromptInput = document.getElementById('customPromptTemplate') as HTMLTextAreaElement;
const modeAppend = document.getElementById('modeAppend') as HTMLInputElement;
const modeReplace = document.getElementById('modeReplace') as HTMLInputElement;

// ============================================
// State
// ============================================

let currentCvId: string | null = null;
let generatedCoverLetter: string | null = null;
let currentPdfBase64: string | null = null;
let activeTab: 'upload' | 'text' = 'upload';

// ============================================
// Initialization
// ============================================

async function init() {
  console.log('[Popup] Initializing state...');
  
  try {
    // Load saved CV
    const cv = await storage.getCv();
    if (cv) {
      console.log('[Popup] Restoring CV from storage:', cv);
      currentCvId = cv.cvId;
      showCvInfo(cv.fileName);
    } else {
      console.log('[Popup] No saved CV in storage');
    }
  } catch (error) {
    console.error('[Popup] Error loading CV:', error);
  }

  try {
    // Load last job data
    const lastJobData = await storage.getLastJobData();
    if (lastJobData) {
      console.log('[Popup] Restoring job data:', lastJobData);
      jobTitleInput.value = lastJobData.jobTitle || '';
      companyNameInput.value = lastJobData.companyName || '';
      jobDescriptionInput.value = lastJobData.jobDescription || '';
    }
  } catch (error) {
    console.error('[Popup] Error loading job data:', error);
  }

  try {
    // Load editor state
    const editorState = await storage.getEditorState();
    if (editorState) {
      console.log('[Popup] Restoring editor state');
      currentPdfBase64 = editorState.pdfBase64;
      latexSourceTextarea.value = editorState.latex;
      updateEditor();
      cvEditorSection.classList.remove('hidden');
    }
  } catch (error) {
    console.error('[Popup] Error loading editor state:', error);
  }

  try {
    // Load cover letter
    const cl = await storage.getGeneratedCoverLetter();
    if (cl) {
      console.log('[Popup] Restoring cover letter');
      generatedCoverLetter = cl;
      resultContent.textContent = cl;
      resultSection.classList.remove('hidden');
    }
  } catch (error) {
    console.error('[Popup] Error loading cover letter:', error);
  }

  try {
    // Load API key status
    const apiKeyStatusData = await api.getGroqApiKey();
    if (apiKeyStatusData) {
      console.log('[Popup] API key found on backend');
      apiKeyStatus.classList.remove('hidden');
    }
  } catch (error) {
    console.warn('[Popup] Could not fetch API key status:', error);
  }

  updateGenerateButtonState();
}

// Auto-save Job Data
function autoSaveJobData() {
  const jobData = {
    jobTitle: jobTitleInput.value,
    companyName: companyNameInput.value,
    jobDescription: jobDescriptionInput.value
  };
  storage.saveLastJobData(jobData);
}

[jobTitleInput, companyNameInput, jobDescriptionInput].forEach(input => {
  input.addEventListener('input', () => {
    autoSaveJobData();
    updateGenerateButtonState();
  });
});

// ============================================
// CV Upload
// ============================================

cvUploadArea.addEventListener('click', () => {
  cvFileInput.click();
});

cvUploadArea.addEventListener('dragover', (e) => {
  e.preventDefault();
  cvUploadArea.style.borderColor = 'var(--primary)';
});

cvUploadArea.addEventListener('dragleave', () => {
  cvUploadArea.style.borderColor = 'var(--border)';
});

cvUploadArea.addEventListener('drop', async (e) => {
  e.preventDefault();
  cvUploadArea.style.borderColor = 'var(--border)';
  
  const files = e.dataTransfer?.files;
  if (files && files.length > 0) {
    await handleCvUpload(files[0]);
  }
});

cvFileInput.addEventListener('change', async (e) => {
  const files = (e.target as HTMLInputElement).files;
  if (files && files.length > 0) {
    await handleCvUpload(files[0]);
  }
});

async function handleCvUpload(file: File) {
  // Validate file type
  const validTypes = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];
  if (!validTypes.includes(file.type)) {
    showError('Please upload a PDF or DOCX file');
    return;
  }

  // Validate file size (5MB max)
  if (file.size > 5 * 1024 * 1024) {
    showError('File size must be less than 5MB');
    return;
  }

  showLoading('Uploading CV...');

  try {
    const response = await api.parseCv(file);
    currentCvId = response.cvId;
    await storage.saveCv(response.cvId, file.name);
    showCvInfo(file.name);
    showSuccess('CV uploaded successfully!');
    updateGenerateButtonState();
  } catch (error) {
    handleApiError(error, 'Failed to upload CV');
  } finally {
    hideLoading();
  }
}

// ============================================
// Tabs Navigation
// ============================================

tabUpload.addEventListener('click', () => {
  activeTab = 'upload';
  tabUpload.classList.add('active');
  tabText.classList.remove('active');
  sectionUpload.classList.remove('hidden');
  sectionText.classList.add('hidden');
  updateGenerateButtonState();
});

tabText.addEventListener('click', () => {
  activeTab = 'text';
  tabText.classList.add('active');
  tabUpload.classList.remove('active');
  sectionText.classList.remove('hidden');
  sectionUpload.classList.add('hidden');
  updateGenerateButtonState();
});

function showCvInfo(fileName: string) {
  uploadPlaceholder.classList.add('hidden');
  cvInfo.classList.remove('hidden');
  cvFileName.textContent = fileName;
}

function hideCvInfo() {
  uploadPlaceholder.classList.remove('hidden');
  cvInfo.classList.add('hidden');
}

deleteCvBtn.addEventListener('click', async (e) => {
  e.stopPropagation();
  await storage.deleteCv();
  currentCvId = null;
  hideCvInfo();
  updateGenerateButtonState();
  showSuccess('CV removed');
});

// ============================================
// CV Customization
// ============================================

customizeCvBtn.addEventListener('click', async (e) => {
  e.stopPropagation();
  
  if (!currentCvId) {
    showError('Please upload your CV first');
    return;
  }

  const jobTitle = jobTitleInput.value.trim();
  const companyName = companyNameInput.value.trim();
  const jobDescription = jobDescriptionInput.value.trim();

  if (!jobTitle || !companyName || !jobDescription) {
    showError('Please fill in job details to tailor your CV');
    return;
  }

  showLoading('Generating customized CV...');

  try {
    const fullJobDesc = `Job Title: ${jobTitle}\nCompany: ${companyName}\n\nJob Description:\n${jobDescription}`;
    const response = await api.customizeCv(currentCvId, fullJobDesc);
    
    // Store results
    currentPdfBase64 = response.pdfContent;
    latexSourceTextarea.value = response.latexSource;
    updateEditor(); // Update highlighting
    
    // Persist editor state
    await storage.saveEditorState(response.latexSource, response.pdfContent);
    
    // Show editor section
    cvEditorSection.classList.remove('hidden');
    switchToSourceView();
    cvEditorSection.scrollIntoView({ behavior: 'smooth' });
    
    showSuccess('✨ Magic CV generated! You can now view or edit the source.');
  } catch (error) {
    handleApiError(error, 'Failed to customize CV');
  } finally {
    hideLoading();
  }
});

viewPdfBtn.addEventListener('click', () => {
  if (!currentPdfBase64) return;
  
  const byteCharacters = atob(currentPdfBase64);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type: 'application/pdf' });
  
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank');
  // Note: We don't revoke here because the new tab needs to load it. 
  // Browser will clean up when the extension process closes or we can manage it.
});

// Editor Highlighting Logic
function updateEditor() {
  let code = latexSourceTextarea.value;
  if (code[code.length-1] == "\n") {
    code += " ";
  }
  highlightingContent.textContent = code;
  if ((window as any).Prism) {
    (window as any).Prism.highlightElement(highlightingContent);
  }
  
  // Persist manual edits
  storage.saveEditorState(latexSourceTextarea.value, currentPdfBase64);
}

latexSourceTextarea.addEventListener('input', updateEditor);

latexSourceTextarea.addEventListener('scroll', () => {
  highlighting.scrollTop = latexSourceTextarea.scrollTop;
  highlighting.scrollLeft = latexSourceTextarea.scrollLeft;
});

// CV Editor Views
btnViewSource.addEventListener('click', switchToSourceView);
btnViewPreview.addEventListener('click', switchToPreviewView);

function switchToSourceView() {
  btnViewSource.classList.add('active');
  btnViewPreview.classList.remove('active');
  sourceEditor.classList.remove('hidden');
  pdfMessage.classList.add('hidden');
  
  // Style pill buttons
  btnViewSource.style.background = 'white';
  btnViewSource.style.boxShadow = '0 2px 4px rgba(0,0,0,0.05)';
  btnViewPreview.style.background = 'transparent';
  btnViewPreview.style.boxShadow = 'none';
}

function switchToPreviewView() {
  btnViewPreview.classList.add('active');
  btnViewSource.classList.remove('active');
  sourceEditor.classList.add('hidden');
  pdfMessage.classList.remove('hidden');
  
  // Style pill buttons
  btnViewPreview.style.background = 'white';
  btnViewPreview.style.boxShadow = '0 2px 4px rgba(0,0,0,0.05)';
  btnViewSource.style.background = 'transparent';
  btnViewSource.style.boxShadow = 'none';
}

// Editor Actions
recompileBtn.addEventListener('click', async () => {
  const source = latexSourceTextarea.value.trim();
  if (!source) return;

  showLoading('Re-compiling PDF...');
  try {
    const pdfBlob = await api.compileLatex(source);
    const reader = new FileReader();
    reader.onloadend = () => {
      currentPdfBase64 = (reader.result as string).split(',')[1];
      
      // Persist state
      storage.saveEditorState(source, currentPdfBase64);
      
      switchToPreviewView();
      showSuccess('PDF updated!');
    };
    reader.readAsDataURL(pdfBlob);
  } catch (error) {
    handleApiError(error, 'Compilation failed');
  } finally {
    hideLoading();
  }
});

downloadTexBtn.addEventListener('click', () => {
  const source = latexSourceTextarea.value.trim();
  if (!source) return;
  const blob = new Blob([source], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `cv_${companyNameInput.value.replace(/\s+/g, '_').toLowerCase()}.tex`;
  a.click();
  URL.revokeObjectURL(url);
});

downloadPdfBtn.addEventListener('click', () => {
  if (!currentPdfBase64) return;
  downloadPdf(currentPdfBase64, `cv_${companyNameInput.value.replace(/\s+/g, '_').toLowerCase()}.pdf`);
});

overleafBtn.addEventListener('click', () => {
  const source = latexSourceTextarea.value.trim();
  if (!source) return;

  const form = document.createElement('form');
  form.method = 'POST';
  form.action = 'https://www.overleaf.com/docs';
  form.target = '_blank';

  const snip = document.createElement('input');
  snip.type = 'hidden';
  snip.name = 'encoded_snip';
  snip.value = encodeURIComponent(source);

  const fileName = document.createElement('input');
  fileName.type = 'hidden';
  fileName.name = 'snip_name';
  fileName.value = 'Resume.tex';

  form.appendChild(snip);
  form.appendChild(fileName);
  document.body.appendChild(form);
  form.submit();
  document.body.removeChild(form);
});

function downloadPdf(base64: string, fileName: string) {
  const byteCharacters = atob(base64);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type: 'application/pdf' });
  
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

// ============================================
// Job Extraction
// ============================================

extractJobBtn.addEventListener('click', async () => {
  showLoading('Extracting job data...');

  try {
    // Get active tab
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

    if (!tab.id) {
      throw new Error('No active tab found');
    }

    // Check if it's a LinkedIn page
    if (!tab.url?.includes('linkedin.com/jobs/')) {
      showError('Please navigate to a LinkedIn job posting first');
      return;
    }

    // Send message to content script
    let response;
    try {
      response = await chrome.tabs.sendMessage(tab.id, { type: 'EXTRACT_JOB_DATA' } as ChromeMessage);
    } catch (msgError: any) {
      if (msgError.message.includes('Could not establish connection')) {
        console.log('[Popup] Connection lost, attempting to re-inject script...');
        // Re-inject the script
        await chrome.scripting.executeScript({
          target: { tabId: tab.id },
          files: ['content/linkedin-detector.js']
        });
        // Try one more time
        response = await chrome.tabs.sendMessage(tab.id, { type: 'EXTRACT_JOB_DATA' } as ChromeMessage);
      } else {
        throw msgError;
      }
    }

    if (response.type === 'ERROR') {
      showError(response.error || 'Failed to extract job data');
      return;
    }

    const jobData: JobData = response.payload;
    jobTitleInput.value = jobData.jobTitle;
    companyNameInput.value = jobData.companyName;
    jobDescriptionInput.value = jobData.jobDescription;

    await storage.saveLastJobData(jobData);
    showSuccess('Job data extracted!');
    updateGenerateButtonState();
  } catch (error: any) {
    console.error('Extract job error:', error);
    showError('Could not reach LinkedIn page. Please refresh the page manually.');
  } finally {
    hideLoading();
  }
});

// Monitor input changes
[jobTitleInput, companyNameInput, jobDescriptionInput, cvTextInput].forEach(input => {
  input.addEventListener('input', updateGenerateButtonState);
});

// ============================================
// Cover Letter Generation
// ============================================

generateBtn.addEventListener('click', async () => {
  const jobTitle = jobTitleInput.value.trim();
  const companyName = companyNameInput.value.trim();
  const jobDescription = jobDescriptionInput.value.trim();
  const customPrompt = customPromptInput.value.trim();
  const promptMode = modeReplace.checked ? 1 : 0;
  const idempotencyKey = crypto.randomUUID();

  if (!jobTitle || !companyName || !jobDescription) {
    showError('Please fill in all job details');
    return;
  }

  showLoading('Generating cover letter...');

  try {
    let response;
    const fullJobDesc = `Job Title: ${jobTitle}\nCompany: ${companyName}\n\nJob Description:\n${jobDescription}`;

    if (activeTab === 'upload') {
      if (!currentCvId) {
        showError('Please upload your CV first');
        hideLoading();
        return;
      }
      response = await api.generateCoverLetter({
        cvId: currentCvId,
        jobDescription: fullJobDesc,
        customPromptTemplate: customPrompt || null,
        promptMode,
        idempotencyKey
      });
    } else {
      const cvText = cvTextInput.value.trim();
      if (!cvText) {
        showError('Please paste your CV text first');
        hideLoading();
        return;
      }
      response = await api.generateCoverLetterFromText({
        cvText,
        jobDescription: fullJobDesc,
        customPromptTemplate: customPrompt || null,
        promptMode,
        idempotencyKey
      });
    }

    generatedCoverLetter = response.coverLetter;
    resultContent.textContent = response.coverLetter;
    
    // Persist cover letter
    await storage.saveGeneratedCoverLetter(response.coverLetter);
    
    resultSection.classList.remove('hidden');
    resultSection.scrollIntoView({ behavior: 'smooth' });
    showSuccess('Cover letter generated!');
  } catch (error) {
    handleApiError(error, 'Failed to generate cover letter');
  } finally {
    hideLoading();
  }
});

// ============================================
// Result Actions
// ============================================

copyBtn.addEventListener('click', async () => {
  if (!generatedCoverLetter) return;

  try {
    await navigator.clipboard.writeText(generatedCoverLetter);
    showSuccess('Copied to clipboard!');
  } catch (error) {
    showError('Failed to copy to clipboard');
  }
});

downloadBtn.addEventListener('click', () => {
  if (!generatedCoverLetter) return;

  const blob = new Blob([generatedCoverLetter], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `cover-letter-${jobTitleInput.value.replace(/\s+/g, '-').toLowerCase()}.txt`;
  a.click();
  URL.revokeObjectURL(url);
  showSuccess('Downloaded!');
});

// ============================================
// Settings
// ============================================

saveApiKeyBtn.addEventListener('click', async () => {
  const apiKey = apiKeyInput.value.trim();

  if (!apiKey || apiKey === '••••••••••••••••') {
    showError('Please enter a valid API key');
    return;
  }

  showLoading('Saving API key...');

  try {
    await api.saveGroqApiKey(apiKey);
    await storage.saveApiKey(apiKey);
    apiKeyInput.value = '••••••••••••••••';
    apiKeyStatus.classList.remove('hidden');
    showSuccess('API key saved!');
  } catch (error) {
    handleApiError(error, 'Failed to save API key');
  } finally {
    hideLoading();
  }
});

deleteApiKeyBtn.addEventListener('click', async () => {
  showLoading('Deleting API key...');

  try {
    await api.deleteGroqApiKey();
    await storage.deleteApiKey();
    apiKeyInput.value = '';
    apiKeyStatus.classList.add('hidden');
    showSuccess('API key deleted');
  } catch (error) {
    handleApiError(error, 'Failed to delete API key');
  } finally {
    hideLoading();
  }
});

// ============================================
// UI Helpers
// ============================================

function updateGenerateButtonState() {
  const hasJobTitle = jobTitleInput.value.trim().length > 0;
  const hasCompany = companyNameInput.value.trim().length > 0;
  const hasDescription = jobDescriptionInput.value.trim().length > 0;
  
  let hasCVSource = false;
  if (activeTab === 'upload') {
    hasCVSource = !!currentCvId;
  } else {
    hasCVSource = cvTextInput.value.trim().length > 0;
  }

  console.log('[Popup] Button State Check:', { activeTab, hasCVSource, hasJobTitle, hasCompany, hasDescription, currentCvId });
  generateBtn.disabled = !(hasCVSource && hasJobTitle && hasCompany && hasDescription);
  
  // Magic Button specifically needs a CV and Job details
  customizeCvBtn.disabled = !(currentCvId && hasJobTitle && hasCompany && hasDescription);
}

function showLoading(text: string) {
  loadingText.textContent = text;
  loadingOverlay.classList.remove('hidden');
}

function hideLoading() {
  loadingOverlay.classList.add('hidden');
}

function showError(message: string) {
  errorMessage.textContent = message;
  errorToast.classList.remove('hidden');
  setTimeout(() => {
    errorToast.classList.add('hidden');
  }, 4000);
}

function showSuccess(message: string) {
  successMessage.textContent = message;
  successToast.classList.remove('hidden');
  setTimeout(() => {
    successToast.classList.add('hidden');
  }, 3000);
}

function handleApiError(error: any, fallbackMessage: string) {
  console.error('API Error:', error);

  if (error instanceof api.ApiClientError) {
    const apiError = error.apiError;
    if (error.status === 429) {
      showError('Rate limit exceeded. Please add your API key in settings.');
    } else if (error.status === 404) {
      showError('CV not found. Please upload your CV again.');
    } else if (apiError && apiError.errors) {
      // Validation errors can be string[] or Record<string, string[]>
      if (Array.isArray(apiError.errors)) {
        showError(apiError.errors[0] || fallbackMessage);
      } else {
        const errorValues = Object.values(apiError.errors);
        if (errorValues.length > 0 && Array.isArray(errorValues[0])) {
          showError(errorValues[0][0]);
        } else {
          showError(fallbackMessage);
        }
      }
    } else if (apiError) {
      showError(apiError.detail || apiError.title || fallbackMessage);
    } else {
      showError(fallbackMessage);
    }
  } else {
    showError(fallbackMessage);
  }
}

// ============================================
// Start
// ============================================

init();
