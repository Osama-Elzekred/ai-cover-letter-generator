// ============================================
// LinkedIn Job Data Detector - Content Script
// ============================================

// Inline type definitions (can't import in content scripts)
interface JobData {
  jobTitle: string;
  companyName: string;
  jobDescription: string;
}

interface ChromeMessage<T = any> {
  type: string;
  payload?: T;
  error?: string;
}

/**
 * Extract job data from LinkedIn job posting page
 */
function extractJobData(): JobData | null {
  try {
    // Try to extract job title
    const jobTitleElement = document.querySelector(
      '.job-details-jobs-unified-top-card__job-title, .jobs-unified-top-card__job-title, h1.t-24'
    );
    const jobTitle = jobTitleElement?.textContent?.trim() || '';

    // Try to extract company name
    const companyElement = document.querySelector(
      '.job-details-jobs-unified-top-card__company-name, .jobs-unified-top-card__company-name, .job-details-jobs-unified-top-card__primary-description a'
    );
    const companyName = companyElement?.textContent?.trim() || '';

    // Try to extract job description
    const descriptionElement = document.querySelector(
      '.jobs-description__content, .jobs-box__html-content, .jobs-description'
    );
    const jobDescription = descriptionElement?.textContent?.trim() || '';

    // Validate that we got all required data
    if (!jobTitle || !companyName || !jobDescription) {
      console.warn('[LinkedIn Detector] Missing required job data:', {
        hasTitle: !!jobTitle,
        hasCompany: !!companyName,
        hasDescription: !!jobDescription,
      });
      return null;
    }

    return {
      jobTitle,
      companyName,
      jobDescription,
    };
  } catch (error) {
    console.error('[LinkedIn Detector] Error extracting job data:', error);
    return null;
  }
}

/**
 * Check if current page is a LinkedIn job posting
 */
function isLinkedInJobPage(): boolean {
  return window.location.href.includes('linkedin.com/jobs/');
}

/**
 * Listen for messages from popup/background
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  if (message.type === 'EXTRACT_JOB_DATA') {
    if (!isLinkedInJobPage()) {
      sendResponse({
        type: 'ERROR',
        error: 'Not a LinkedIn job page',
      } as ChromeMessage);
      return true;
    }

    const jobData = extractJobData();

    if (jobData) {
      sendResponse({
        type: 'JOB_DATA_EXTRACTED',
        payload: jobData,
      } as ChromeMessage<JobData>);
    } else {
      sendResponse({
        type: 'ERROR',
        error: 'Could not extract job data. Make sure you are on a LinkedIn job posting page.',
      } as ChromeMessage);
    }

    return true; // Keep message channel open for async response
  }
});

console.log('[LinkedIn Detector] Content script loaded');
