// ============================================
// Chrome Storage Utility Functions
// ============================================

import type { StorageData } from '../types/index.js';

/**
 * Get or generate a unique user ID
 */
export async function getUserId(): Promise<string> {
  const data = await chrome.storage.local.get('userId');
  
  if (data.userId) {
    return data.userId;
  }
  
  // Generate new UUID
  const userId = crypto.randomUUID();
  await chrome.storage.local.set({ userId });
  return userId;
}

/**
 * Save CV data to storage
 */
export async function saveCv(id: string, fileName: string): Promise<void> {
  console.log('[Storage] Saving CV:', { id, fileName });
  await chrome.storage.local.set({ cvId: id, cvFileName: fileName });
}

/**
 * Get stored CV data
 */
export async function getCv(): Promise<{ cvId: string; fileName: string } | null> {
  const data = await chrome.storage.local.get(['cvId', 'cvFileName']);
  console.log('[Storage] Getting CV from storage:', data);
  
  if (data.cvId && data.cvFileName) {
    return { cvId: data.cvId, fileName: data.cvFileName };
  }
  
  return null;
}

/**
 * Delete CV data from storage
 */
export async function deleteCv(): Promise<void> {
  await chrome.storage.local.remove(['cvId', 'cvFileName']);
}

/**
 * Save Groq API key
 */
export async function saveApiKey(apiKey: string): Promise<void> {
  await chrome.storage.local.set({ apiKey });
}

/**
 * Get stored API key
 */
export async function getApiKey(): Promise<string | null> {
  const data = await chrome.storage.local.get('apiKey');
  return data.apiKey || null;
}

/**
 * Delete API key
 */
export async function deleteApiKey(): Promise<void> {
  await chrome.storage.local.remove('apiKey');
}

/**
 * Save last extracted job data
 */
export async function saveLastJobData(jobData: { jobTitle: string; companyName: string; jobDescription: string }): Promise<void> {
  await chrome.storage.local.set({ lastJobData: jobData });
}

/**
 * Get last extracted job data
 */
export async function getLastJobData(): Promise<{ jobTitle: string; companyName: string; jobDescription: string } | null> {
  const data = await chrome.storage.local.get('lastJobData');
  return data.lastJobData || null;
}

/**
 * Save Editor state (LaTeX and PDF)
 */
export async function saveEditorState(latex: string, pdfBase64: string | null): Promise<void> {
  await chrome.storage.local.set({ 
    editorState: { 
      latex, 
      pdfBase64,
      updatedAt: Date.now()
    } 
  });
}

/**
 * Get stored Editor state
 */
export async function getEditorState(): Promise<{ latex: string; pdfBase64: string | null } | null> {
  const data = await chrome.storage.local.get('editorState');
  return data.editorState || null;
}

/**
 * Delete Editor state
 */
export async function deleteEditorState(): Promise<void> {
  await chrome.storage.local.remove('editorState');
}

/**
 * Save generated cover letter
 */
export async function saveGeneratedCoverLetter(text: string): Promise<void> {
  await chrome.storage.local.set({ generatedCoverLetter: text });
}

/**
 * Get generated cover letter
 */
export async function getGeneratedCoverLetter(): Promise<string | null> {
  const data = await chrome.storage.local.get('generatedCoverLetter');
  return data.generatedCoverLetter || null;
}

/**
 * Clear all storage data
 */
export async function clearAllData(): Promise<void> {
  await chrome.storage.local.clear();
}

/**
 * Get all storage data (for debugging)
 */
export async function getAllData(): Promise<StorageData> {
  return await chrome.storage.local.get(null) as StorageData;
}

/**
 * Get onboarding completion status
 */
export async function getOnboardingCompleted(): Promise<boolean> {
  const data = await chrome.storage.local.get('onboardingCompleted');
  return data.onboardingCompleted === true;
}

/**
 * Set onboarding completion status
 */
export async function setOnboardingCompleted(completed: boolean): Promise<void> {
  await chrome.storage.local.set({ onboardingCompleted: completed });
}

/**
 * Get usage count for current rate limit window
 */
export async function getUsageCount(): Promise<number> {
  const data = await chrome.storage.local.get('usageCount');
  return data.usageCount || 0;
}

/**
 * Increment usage count
 */
export async function incrementUsageCount(): Promise<number> {
  const currentCount = await getUsageCount();
  const newCount = currentCount + 1;
  await chrome.storage.local.set({ usageCount: newCount });
  return newCount;
}

/**
 * Reset usage count (called on new rate limit window)
 */
export async function resetUsageCount(): Promise<void> {
  await chrome.storage.local.set({ usageCount: 0 });
}

/**
 * Get BYOK banner dismissed status
 */
export async function getByokBannerDismissed(): Promise<boolean> {
  const data = await chrome.storage.local.get('byokBannerDismissed');
  return data.byokBannerDismissed === true;
}

/**
 * Set BYOK banner dismissed status
 */
export async function setByokBannerDismissed(dismissed: boolean): Promise<void> {
  await chrome.storage.local.set({ byokBannerDismissed: dismissed });
}
