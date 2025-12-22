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

// ============================================
// State
// ============================================

let currentCvId: string | null = null;
let generatedCoverLetter: string | null = null;

// ============================================
// Initialization
// ============================================

async function init() {
  // Load saved CV
  const cv = await storage.getCv();
  if (cv) {
    currentCvId = cv.cvId;
    showCvInfo(cv.fileName);
  }

  // Load last job data
  const lastJobData = await storage.getLastJobData();
  if (lastJobData) {
    jobTitleInput.value = lastJobData.jobTitle;
    companyNameInput.value = lastJobData.companyName;
    jobDescriptionInput.value = lastJobData.jobDescription;
  }

  // Load API key status
  const apiKey = await storage.getApiKey();
  if (apiKey) {
    apiKeyInput.value = '••••••••••••••••';
    apiKeyStatus.classList.remove('hidden');
  }

  updateGenerateButtonState();
}

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
    const response = await chrome.tabs.sendMessage(tab.id, {
      type: 'EXTRACT_JOB_DATA',
    } as ChromeMessage);

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
  } catch (error) {
    console.error('Extract job error:', error);
    showError('Failed to extract job data. Make sure you are on a LinkedIn job page.');
  } finally {
    hideLoading();
  }
});

// Monitor input changes
[jobTitleInput, companyNameInput, jobDescriptionInput].forEach(input => {
  input.addEventListener('input', updateGenerateButtonState);
});

// ============================================
// Cover Letter Generation
// ============================================

generateBtn.addEventListener('click', async () => {
  if (!currentCvId) {
    showError('Please upload your CV first');
    return;
  }

  const jobTitle = jobTitleInput.value.trim();
  const companyName = companyNameInput.value.trim();
  const jobDescription = jobDescriptionInput.value.trim();

  if (!jobTitle || !companyName || !jobDescription) {
    showError('Please fill in all job details');
    return;
  }

  showLoading('Generating cover letter...');

  try {
    const request: CoverLetterRequest = {
      cvId: currentCvId,
      jobTitle,
      companyName,
      jobDescription,
    };

    const response = await api.generateCoverLetter(request);
    generatedCoverLetter = response.coverLetter;
    
    resultContent.textContent = response.coverLetter;
    resultSection.classList.remove('hidden');
    
    // Scroll to result
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
  const hasCV = !!currentCvId;
  const hasJobTitle = jobTitleInput.value.trim().length > 0;
  const hasCompany = companyNameInput.value.trim().length > 0;
  const hasDescription = jobDescriptionInput.value.trim().length > 0;

  generateBtn.disabled = !(hasCV && hasJobTitle && hasCompany && hasDescription);
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

function handleApiError(error: unknown, fallbackMessage: string) {
  console.error('API Error:', error);

  if (error instanceof api.ApiClientError) {
    if (error.status === 429) {
      showError('Rate limit exceeded. Please add your API key in settings.');
    } else if (error.status === 404) {
      showError('CV not found. Please upload your CV again.');
    } else if (error.apiError.errors) {
      // Validation errors
      const firstError = Object.values(error.apiError.errors)[0][0];
      showError(firstError);
    } else {
      showError(error.apiError.detail || error.apiError.title);
    }
  } else {
    showError(fallbackMessage);
  }
}

// ============================================
// Start
// ============================================

init();
