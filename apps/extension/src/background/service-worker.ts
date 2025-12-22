// ============================================
// Background Service Worker
// ============================================

// Inline type definition
interface ChromeMessage<T = any> {
  type: string;
  payload?: T;
  error?: string;
}

/**
 * Handle messages from popup and content scripts
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  console.log('[Service Worker] Received message:', message.type);

  // Forward messages between popup and content scripts
  // Most logic will be handled in popup.ts directly
  
  return false; // Synchronous response
});

/**
 * Handle extension installation
 */
chrome.runtime.onInstalled.addListener((details) => {
  if (details.reason === 'install') {
    console.log('[Service Worker] Extension installed');
  } else if (details.reason === 'update') {
    console.log('[Service Worker] Extension updated');
  }
});

console.log('[Service Worker] Background script loaded');
