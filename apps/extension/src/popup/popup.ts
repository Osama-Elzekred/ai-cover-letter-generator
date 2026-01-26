// ============================================
// Popup UI Logic
// ============================================

import type { JobData, ChromeMessage, CoverLetterRequest } from '../types/index.js';
import * as storage from '../utils/storage.js';
import * as api from '../utils/api-client.js';
import { initOnboarding, restartOnboarding } from './onboarding.js';

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
const collapseCvBtn = document.getElementById('collapseCvBtn') as HTMLButtonElement;
const cvSummary = document.getElementById('cvSummary') as HTMLDivElement;
const cvSummaryDetail = document.getElementById('cvSummaryDetail') as HTMLDivElement;
const cvReplaceBtn = document.getElementById('cvReplaceBtn') as HTMLButtonElement;
const cvRemoveBtn = document.getElementById('cvRemoveBtn') as HTMLButtonElement;
const cvInputBlock = document.getElementById('cvInputBlock') as HTMLDivElement;

const extractJobBtn = document.getElementById('extractJobBtn') as HTMLButtonElement;
const jobTitleInput = document.getElementById('jobTitle') as HTMLInputElement;
const companyNameInput = document.getElementById('companyName') as HTMLInputElement;
const jobDescriptionInput = document.getElementById('jobDescription') as HTMLTextAreaElement;

const generateBtn = document.getElementById('generateBtn') as HTMLButtonElement;

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
const tabInput = document.getElementById('tabInput') as HTMLButtonElement;
const tabResults = document.getElementById('tabResults') as HTMLButtonElement;
const tabSettings = document.getElementById('tabSettings') as HTMLButtonElement;
const sectionInput = document.getElementById('sectionInput') as HTMLElement;
const sectionResults = document.getElementById('sectionResults') as HTMLElement;
const sectionSettings = document.getElementById('sectionSettings') as HTMLElement;

// Result subsections
const coverLetterResult = document.getElementById('coverLetterResult') as HTMLElement;
const cvEditorResult = document.getElementById('cvEditorResult') as HTMLElement;
const btnResultCoverLetter = document.getElementById('btnResultCoverLetter') as HTMLButtonElement;
const btnResultCv = document.getElementById('btnResultCv') as HTMLButtonElement;

// Text Input
const cvTextInput = document.getElementById('cvText') as HTMLTextAreaElement;
const cvTextArea = document.getElementById('cvTextArea') as HTMLDivElement;
const btnCvUpload = document.getElementById('btnCvUpload') as HTMLButtonElement;
const btnCvText = document.getElementById('btnCvText') as HTMLButtonElement;

// Advanced Options
const customPromptInput = document.getElementById('customPromptTemplate') as HTMLTextAreaElement;
const modeAppend = document.getElementById('modeAppend') as HTMLInputElement;
const modeReplace = document.getElementById('modeReplace') as HTMLInputElement;

// Settings Tab Elements
const promptTypeSelect = document.getElementById('promptTypeSelect') as HTMLSelectElement;
const customPromptEditor = document.getElementById('customPromptEditor') as HTMLTextAreaElement;
const savePromptBtn = document.getElementById('savePromptBtn') as HTMLButtonElement;
const loadPromptBtn = document.getElementById('loadPromptBtn') as HTMLButtonElement;
const viewDefaultBtn = document.getElementById('viewDefaultBtn') as HTMLButtonElement;
const deletePromptBtn = document.getElementById('deletePromptBtn') as HTMLButtonElement;
const promptStatus = document.getElementById('promptStatus') as HTMLDivElement;
const promptStatusText = document.getElementById('promptStatusText') as HTMLSpanElement;

// Onboarding & BYOK elements
const usageBadge = document.getElementById('usageBadge') as HTMLDivElement;
const usageText = document.getElementById('usageText') as HTMLSpanElement;
const byokBanner = document.getElementById('byokBanner') as HTMLDivElement;
const byokBannerAction = document.getElementById('byokBannerAction') as HTMLButtonElement;
const byokBannerClose = document.getElementById('byokBannerClose') as HTMLButtonElement;
const getApiKeyBtn = document.getElementById('getApiKeyBtn') as HTMLButtonElement;
const restartTutorialBtn = document.getElementById('restartTutorialBtn') as HTMLButtonElement;

// ============================================
// State
// ============================================

let currentCvId: string | null = null;
let generatedCoverLetter: string | null = null;
let currentPdfBase64: string | null = null;
let activeTab: 'input' | 'results' | 'settings' = 'input';
let activeResultTab: 'cover' | 'cv' = 'cover';
let hasCoverLetterResult = false;
let hasCvResult = false;
let cvCollapsed = false;
let cvFileDisplayName: string | null = null;

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
      cvCollapsed = true;
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
      hasCvResult = true;
      setResultTab('cv');
      cvCollapsed = true;
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
      hasCoverLetterResult = true;
      setResultTab('cover');
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
  await updateUsageChip();
  updateResultsVisibility();
  updateCvUI();

  // Check and show onboarding for first-time users
  await initOnboarding();
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
  const validTypes = ['application/pdf', 'text/plain'];
  if (!validTypes.includes(file.type)) {
    showError('Please upload a PDF or TXT file');
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
    cvCollapsed = true;
    showSuccess('CV uploaded successfully!');
    updateGenerateButtonState();
    updateCvUI();
  } catch (error) {
    handleApiError(error, 'Failed to upload CV');
  } finally {
    hideLoading();
  }
}

// ============================================
// Tabs Navigation
// ============================================

function switchToUploadMode() {
  btnCvUpload.classList.add('active');
  btnCvText.classList.remove('active');
  cvUploadArea.classList.remove('hidden');
  cvTextArea.classList.add('hidden');
  cvCollapsed = false;
  updateGenerateButtonState();
  updateCvUI();
}

function switchToTextMode() {
  btnCvText.classList.add('active');
  btnCvUpload.classList.remove('active');
  cvUploadArea.classList.add('hidden');
  cvTextArea.classList.remove('hidden');
  cvCollapsed = false;
  updateGenerateButtonState();
  updateCvUI();
}

btnCvUpload.addEventListener('click', switchToUploadMode);
btnCvText.addEventListener('click', switchToTextMode);

tabInput.addEventListener('click', () => {
  activeTab = 'input';
  tabInput.classList.add('active');
  tabResults.classList.remove('active');
  tabSettings.classList.remove('active');
  sectionInput.classList.remove('hidden');
  sectionResults.classList.add('hidden');
  sectionSettings.classList.add('hidden');
  updateGenerateButtonState();
});

tabResults.addEventListener('click', () => {
  activeTab = 'results';
  tabResults.classList.add('active');
  tabInput.classList.remove('active');
  tabSettings.classList.remove('active');
  sectionResults.classList.remove('hidden');
  sectionInput.classList.add('hidden');
  sectionSettings.classList.add('hidden');
  updateResultsVisibility();
});

tabSettings.addEventListener('click', () => {
  activeTab = 'settings';
  tabSettings.classList.add('active');
  tabInput.classList.remove('active');
  tabResults.classList.remove('active');
  sectionSettings.classList.remove('hidden');
  sectionInput.classList.add('hidden');
  sectionResults.classList.add('hidden');
  // Load current prompt when opening settings
  loadCurrentPrompt();
});

// Nested Results tabs (Cover Letter / CV)
function setResultTab(tab: 'cover' | 'cv') {
  activeResultTab = tab;
  btnResultCoverLetter.classList.toggle('active', tab === 'cover');
  btnResultCv.classList.toggle('active', tab === 'cv');
  updateResultsVisibility();
}

function updateResultsVisibility() {
  const showCover = hasCoverLetterResult && activeResultTab === 'cover';
  const showCv = hasCvResult && activeResultTab === 'cv';

  coverLetterResult.classList.toggle('hidden', !showCover);
  cvEditorResult.classList.toggle('hidden', !showCv);

  btnResultCoverLetter.disabled = !hasCoverLetterResult;
  btnResultCv.disabled = !hasCvResult;

   // Only show the Results section when the Results tab is active
  if (activeTab === 'results') {
    sectionResults.classList.remove('hidden');
  } else {
    sectionResults.classList.add('hidden');
  }
}

btnResultCoverLetter.addEventListener('click', () => setResultTab('cover'));
btnResultCv.addEventListener('click', () => setResultTab('cv'));

function showCvInfo(fileName: string) {
  uploadPlaceholder.classList.add('hidden');
  cvInfo.classList.remove('hidden');
  cvFileName.textContent = fileName;
  cvFileDisplayName = fileName;
}

function hideCvInfo() {
  uploadPlaceholder.classList.remove('hidden');
  cvInfo.classList.add('hidden');
  cvFileDisplayName = null;
}

deleteCvBtn.addEventListener('click', async (e) => {
  e.stopPropagation();
  await removeCv();
});

async function removeCv() {
  await storage.deleteCv();
  currentCvId = null;
  cvFileDisplayName = null;
  hideCvInfo();
  cvTextInput.value = '';
  cvCollapsed = false;
  updateGenerateButtonState();
  updateCvUI();
  showSuccess('CV removed');
}

collapseCvBtn.addEventListener('click', async () => {
  const hasFile = !!currentCvId;
  const hasText = cvTextInput.value.trim().length > 0;
  
  if (!hasFile && !hasText) {
    showError('Upload a file or paste your CV first');
    return;
  }
  
  // If using text mode and no CV ID yet, parse the text first
  if (!hasFile && hasText) {
    showLoading('Saving CV text...');
    try {
      const response = await api.parseCvText(cvTextInput.value.trim());
      currentCvId = response.cvId;
      await storage.saveCv(response.cvId, 'cv.txt');
      cvFileDisplayName = 'CV Text';
      showSuccess('CV text saved successfully!');
    } catch (error) {
      handleApiError(error, 'Failed to save CV text');
      hideLoading();
      return;
    } finally {
      hideLoading();
    }
  }
  
  cvCollapsed = true;
  updateCvUI();
});

cvReplaceBtn.addEventListener('click', () => {
  cvCollapsed = false;
  updateCvUI();
  cvInputBlock.scrollIntoView({ behavior: 'smooth', block: 'center' });
});

cvRemoveBtn.addEventListener('click', async () => {
  await removeCv();
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
    hasCvResult = true;
    
    // Persist editor state
    await storage.saveEditorState(response.latexSource, response.pdfContent);
    
    // Show editor result and switch to Results tab
    sectionResults.classList.remove('hidden');
    tabResults.click();
    setResultTab('cv');
    updateResultsVisibility();
    
    setTimeout(() => cvEditorResult.scrollIntoView({ behavior: 'smooth' }), 100);
    switchToSourceView();
    
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

    // Check if using CV ID (file upload) or direct text
    if (currentCvId) {
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
    hasCoverLetterResult = true;
    
    // Persist cover letter
    await storage.saveGeneratedCoverLetter(response.coverLetter);
    
    // Show cover letter result and switch to Results tab
    sectionResults.classList.remove('hidden');
    tabResults.click();
    setResultTab('cover');
    updateResultsVisibility();
    
    setTimeout(() => coverLetterResult.scrollIntoView({ behavior: 'smooth' }), 100);
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
    await updateUsageChip();
    byokBanner.classList.add('hidden');
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
    await updateUsageChip();
  } catch (error) {
    handleApiError(error, 'Failed to delete API key');
  } finally {
    hideLoading();
  }
});

// ============================================
// UI Helpers
// ============================================

function getCvSummaryText(hasFile: boolean, hasText: boolean) {
  if (hasFile && cvFileDisplayName) {
    // Show file name for uploaded files, or 'CV Text' for text mode
    return `File: ${cvFileDisplayName}`;
  }

  if (hasFile && !cvFileDisplayName) {
    return 'CV ready';
  }

  if (hasText) {
    const text = cvTextInput.value.trim();
    const snippet = text.replace(/\s+/g, ' ').slice(0, 60);
    const suffix = text.length > 60 ? '...' : '';
    return snippet ? `Text: ${snippet}${suffix}` : 'CV text ready';
  }

  return 'No CV yet';
}

function updateCvUI() {
  const hasFile = !!currentCvId;
  const hasText = cvTextInput.value.trim().length > 0;
  const hasCv = hasFile || hasText;

  if (!hasCv) {
    cvCollapsed = false;
  }

  cvSummaryDetail.textContent = getCvSummaryText(hasFile, hasText);
  cvSummary.classList.toggle('hidden', !(cvCollapsed && hasCv));
  cvInputBlock.classList.toggle('hidden', cvCollapsed && hasCv);
  collapseCvBtn.disabled = !hasCv;
}

function updateGenerateButtonState() {
  const hasJobTitle = jobTitleInput.value.trim().length > 0;
  const hasCompany = companyNameInput.value.trim().length > 0;
  const hasDescription = jobDescriptionInput.value.trim().length > 0;
  
  // Check if we have CV source (either uploaded file or pasted text)
  const hasCVSource = !!currentCvId || cvTextInput.value.trim().length > 0;

  console.log('[Popup] Button State Check:', { activeTab, hasCVSource, hasJobTitle, hasCompany, hasDescription, currentCvId });
  generateBtn.disabled = !(hasCVSource && hasJobTitle && hasCompany && hasDescription);
  
  // Magic Button specifically needs a CV ID (uploaded file) and Job details
  customizeCvBtn.disabled = !(currentCvId && hasJobTitle && hasCompany && hasDescription);

  updateCvUI();
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
      showError('Rate limit exceeded. Add your free API key in Settings for unlimited access.');
      // Show BYOK banner on rate limit
      showByokBannerIfNeeded();
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
// Settings Tab - Custom Prompts
// ============================================

// Load saved prompt when prompt type changes
promptTypeSelect.addEventListener('change', () => {
  loadCurrentPrompt();
});

// Load current prompt for selected type
async function loadCurrentPrompt() {
  const promptType = promptTypeSelect.value as 'cv-customization' | 'cover-letter' | 'match-analysis';
  
  try {
    const response: ChromeMessage = await chrome.runtime.sendMessage({
      type: 'GET_CUSTOM_PROMPT',
      payload: { promptType }
    });

    if (response.error) {
      console.error('Error loading prompt:', response.error);
      customPromptEditor.value = '';
    } else if (response.payload?.prompt) {
      customPromptEditor.value = response.payload.prompt;
      showPromptStatus('Loaded saved prompt', 'info');
    } else {
      customPromptEditor.value = '';
      customPromptEditor.placeholder = 'No custom prompt saved. Click "View Default" to see the system prompt.';
    }
  } catch (error) {
    console.error('Error loading prompt:', error);
    customPromptEditor.value = '';
  }
}

// Save custom prompt
savePromptBtn.addEventListener('click', async () => {
  const promptType = promptTypeSelect.value;
  const prompt = customPromptEditor.value.trim();

  if (!prompt) {
    showPromptStatus('Please enter a custom prompt', 'error');
    return;
  }

  try {
    showLoading('Saving custom prompt...');

    const response: ChromeMessage = await chrome.runtime.sendMessage({
      type: 'SAVE_CUSTOM_PROMPT',
      payload: { promptType, prompt }
    });

    if (response.error) {
      showPromptStatus('Failed to save prompt', 'error');
      showError(response.error || 'Failed to save prompt');
    } else {
      showPromptStatus('Custom prompt saved successfully!', 'success');
      showSuccess('Custom prompt saved');
    }
  } catch (error: any) {
    console.error('Error saving prompt:', error);
    showPromptStatus('Error saving prompt', 'error');
    showError(error.message || 'Failed to save prompt');
  } finally {
    hideLoading();
  }
});

// Load saved prompt button
loadPromptBtn.addEventListener('click', () => {
  loadCurrentPrompt();
});

// View default prompt
viewDefaultBtn.addEventListener('click', async () => {
  const promptType = promptTypeSelect.value;
  
  // Map to API prompt types
  const apiPromptTypeMap: Record<string, string> = {
    'cv-customization': 'CvCustomization',
    'cover-letter': 'CoverLetter',
    'match-analysis': 'MatchAnalysis'
  };

  try {
    showLoading('Loading default prompt...');

    const response: ChromeMessage = await chrome.runtime.sendMessage({
      type: 'VIEW_PROMPTS_DIRECT'
    });

    if (response.error) {
      showPromptStatus('Failed to load default prompt', 'error');
    } else if (response.payload) {
      const templates = response.payload;
      const promptType = promptTypeSelect.value;
      
      // Convert kebab-case to camelCase (e.g., 'cv-customization' -> 'cvCustomization')
      const toCamelCase = (str: string) => str.replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
      const templateKey = toCamelCase(promptType);
      const template = templates[templateKey];

      if (template) {
        customPromptEditor.value = template;
        showPromptStatus('Loaded default prompt (read-only view)', 'info');
      } else {
        showPromptStatus('Default prompt not found', 'error');
      }
    }
  } catch (error: any) {
    console.error('Error loading default prompt:', error);
    showPromptStatus('Error loading default prompt', 'error');
  } finally {
    hideLoading();
  }
});

// Delete custom prompt
deletePromptBtn.addEventListener('click', async () => {
  const promptType = promptTypeSelect.value;

  if (!confirm('Are you sure you want to delete this custom prompt? The default prompt will be used instead.')) {
    return;
  }

  try {
    showLoading('Deleting custom prompt...');

    const response: ChromeMessage = await chrome.runtime.sendMessage({
      type: 'DELETE_CUSTOM_PROMPT',
      payload: { promptType }
    });

    if (response.error) {
      showPromptStatus('Failed to delete prompt', 'error');
      showError(response.error || 'Failed to delete prompt');
    } else {
      customPromptEditor.value = '';
      showPromptStatus('Custom prompt deleted. Using default.', 'success');
      showSuccess('Custom prompt deleted');
    }
  } catch (error: any) {
    console.error('Error deleting prompt:', error);
    showPromptStatus('Error deleting prompt', 'error');
    showError(error.message || 'Failed to delete prompt');
  } finally {
    hideLoading();
  }
});

function showPromptStatus(message: string, type: 'success' | 'error' | 'info') {
  promptStatusText.textContent = message;
  promptStatus.classList.remove('hidden');
  
  // Color coding
  if (type === 'success') {
    promptStatus.style.color = '#10b981';
  } else if (type === 'error') {
    promptStatus.style.color = '#ef4444';
  } else {
    promptStatus.style.color = '#6366f1';
  }
  
  setTimeout(() => {
    promptStatus.classList.add('hidden');
  }, 3000);
}

// ============================================
// Onboarding & BYOK Features
// ============================================

/**
 * Update limited/unlimited chip in header
 */
async function updateUsageChip() {
  try {
    const apiKey = await storage.getApiKey();

    usageBadge.classList.remove('hidden');

    if (apiKey) {
      usageBadge.classList.add('unlimited');
      usageText.textContent = 'Unlimited';
      usageBadge.title = 'You are using your Groq API key';
    } else {
      usageBadge.classList.remove('unlimited');
      usageText.textContent = 'Limited';
      usageBadge.title = 'Save your free Groq API key for unlimited usage';
    }
  } catch (error) {
    console.error('[Popup] Error updating usage chip:', error);
    usageBadge.classList.add('hidden');
  }
}

/**
 * Show BYOK upgrade banner if not dismissed
 */
async function showByokBannerIfNeeded() {
  const dismissed = await storage.getByokBannerDismissed();
  if (!dismissed) {
    byokBanner.classList.remove('hidden');
  }
}

// Get Free API Key button (Settings tab)
getApiKeyBtn.addEventListener('click', () => {
  window.open('https://console.groq.com/keys', '_blank');
});

// BYOK Banner action button
byokBannerAction.addEventListener('click', () => {
  // Switch to Settings tab
  tabSettings.click();
  // Open Groq API key page
  window.open('https://console.groq.com/keys', '_blank');
  // Hide banner
  byokBanner.classList.add('hidden');
});

// BYOK Banner close button
byokBannerClose.addEventListener('click', async () => {
  byokBanner.classList.add('hidden');
  await storage.setByokBannerDismissed(true);
});

// Restart Tutorial button
restartTutorialBtn.addEventListener('click', async () => {
  await restartOnboarding();
});

// Limited/Unlimited chip click → open Groq key page
usageBadge.addEventListener('click', () => {
  window.open('https://console.groq.com/keys', '_blank');
});

// ============================================
// Start
// ============================================

init();
