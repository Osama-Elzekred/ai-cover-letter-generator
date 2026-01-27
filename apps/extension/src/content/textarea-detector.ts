// ============================================
// LinkedIn Textarea Detection & Icon Injection
// ============================================

interface TextareaMetadata {
  element: HTMLTextAreaElement | HTMLInputElement;
  fieldName: string;
  fieldLabel: string;
  iconContainer: HTMLElement;
}

// Track all injected icons to avoid duplicates
const injectedTextareas = new WeakMap<Element, TextareaMetadata>();

/**
 * LinkedIn textarea selectors for job application forms
 * Detects textareas in apply modals and job description review flows
 */
const LINKEDIN_TEXTAREA_SELECTORS = [
  'textarea[name*="question"]',
  'textarea[data-field-name]',
  'textarea.artdeco-inline-feedback-form__textarea',
  'textarea.ql-editor', // Quill editor used in applications
  'div[contenteditable="true"].ql-editor', // Rich text editor
  'textarea[placeholder*="answer"], textarea[placeholder*="question"], textarea[placeholder*="explain"]',
  'textarea[aria-label*="answer"], textarea[aria-label*="question"], textarea[aria-label*="explain"]',
];

/**
 * Detect all textareas on page and inject icons
 */
export function initializeTextareaDetection(): void {
  // Initial scan
  scanAndInjectTextareaIcons();

  // Watch for dynamically added textareas (LinkedIn modals, etc.)
  const observer = new MutationObserver((mutations) => {
    // Debounce to avoid excessive scanning
    clearTimeout((window as any).textareaScanTimeout);
    (window as any).textareaScanTimeout = setTimeout(() => {
      scanAndInjectTextareaIcons();
    }, 300);
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true,
    attributes: false,
  });

  console.log('[Textarea Detection] Initialized');
}

/**
 * Scan page for textareas and inject icons if not already injected
 */
function scanAndInjectTextareaIcons(): void {
  const textareas = document.querySelectorAll<HTMLTextAreaElement>(
    LINKEDIN_TEXTAREA_SELECTORS.join(', ')
  );

  textareas.forEach((textarea) => {
    if (!injectedTextareas.has(textarea)) {
      injectIconForTextarea(textarea);
    }
  });
}

/**
 * Inject icon next to a single textarea element
 */
function injectIconForTextarea(
  textarea: HTMLTextAreaElement | HTMLInputElement
): void {
  try {
    // Skip if textarea is hidden or has no parent
    if (textarea.offsetParent === null || !textarea.parentElement) {
      return;
    }

    // Extract field information
    const fieldName = textarea.getAttribute('name') || textarea.getAttribute('data-field-name') || 'Answer';
    const fieldLabel = textarea.getAttribute('aria-label') || textarea.getAttribute('placeholder') || fieldName;

    // Create icon container
    const iconContainer = createIconElement();
    const metadata: TextareaMetadata = {
      element: textarea,
      fieldName,
      fieldLabel,
      iconContainer,
    };

    injectedTextareas.set(textarea, metadata);

    // Position icon next to textarea
    const parentContainer = textarea.parentElement;
    parentContainer.style.position = 'relative';

    // Insert icon after textarea or in a dedicated position
    parentContainer.appendChild(iconContainer);

    // Setup click handler
    const button = iconContainer.querySelector('[data-ai-icon-button]') as HTMLElement;
    if (button) {
      button.addEventListener('click', (e) => {
        e.stopPropagation();
        handleIconClick(metadata);
      });
    }

    // Add hover effects
    textarea.addEventListener('focus', () => {
      iconContainer.style.opacity = '1';
    });

    textarea.addEventListener('blur', () => {
      // Keep visible on blur, just less prominent
      iconContainer.style.opacity = '0.7';
    });

    console.log(`[Textarea Detection] Icon injected for field: ${fieldLabel}`);
  } catch (error) {
    console.error('[Textarea Detection] Error injecting icon:', error);
  }
}

/**
 * Create styled icon element
 */
function createIconElement(): HTMLElement {
  const container = document.createElement('div');
  container.setAttribute('data-ai-icon-container', 'true');
  container.innerHTML = `
    <button 
      data-ai-icon-button 
      type="button" 
      title="Generate answer using AI and your CV"
      aria-label="Generate AI answer"
      style="
        position: absolute;
        right: 12px;
        top: 50%;
        transform: translateY(-50%);
        width: 32px;
        height: 32px;
        border: none;
        border-radius: 6px;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 16px;
        padding: 0;
        z-index: 1000;
        opacity: 0.7;
        transition: all 0.2s ease;
        box-shadow: 0 2px 8px rgba(102, 126, 234, 0.2);
      "
    >
      ✨
    </button>
  `;

  // Add hover effect
  const button = container.querySelector('button');
  if (button) {
    button.addEventListener('mouseenter', () => {
      button.style.opacity = '1';
      button.style.transform = 'translateY(-50%) scale(1.1)';
      button.style.boxShadow = '0 4px 12px rgba(102, 126, 234, 0.4)';
    });

    button.addEventListener('mouseleave', () => {
      button.style.opacity = '0.7';
      button.style.transform = 'translateY(-50%) scale(1)';
      button.style.boxShadow = '0 2px 8px rgba(102, 126, 234, 0.2)';
    });
  }

  return container;
}

/**
 * Handle icon click - open modal to ask question
 */
function handleIconClick(metadata: TextareaMetadata): void {
  // Send message to background to prepare modal data
  chrome.runtime.sendMessage(
    {
      type: 'OPEN_TEXTAREA_MODAL',
      payload: {
        fieldName: metadata.fieldName,
        fieldLabel: metadata.fieldLabel,
      },
    },
    (response) => {
      if (response && !response.error) {
        // Modal opened in popup/dedicated context
        console.log('[Textarea Detection] Modal opened for field:', metadata.fieldLabel);
      } else {
        console.error('[Textarea Detection] Error opening modal:', response?.error);
      }
    }
  );
}

/**
 * Inject answer into textarea
 * Called from popup/modal when user confirms generated answer
 */
export function injectAnswerIntoTextarea(answer: string): void {
  // This will be called from the background script
  // which identifies the correct textarea via messaging context
  
  // For now, inject into the last focused textarea
  const focusedElement = document.activeElement as HTMLTextAreaElement | HTMLInputElement;
  
  if (focusedElement && (focusedElement.tagName === 'TEXTAREA' || focusedElement.tagName === 'INPUT')) {
    focusedElement.value = answer;
    
    // Trigger change event so LinkedIn forms register the input
    focusedElement.dispatchEvent(new Event('input', { bubbles: true }));
    focusedElement.dispatchEvent(new Event('change', { bubbles: true }));
    
    console.log('[Textarea Detection] Answer injected into field');
  }
}

/**
 * Get count of detected textareas (for debugging)
 */
export function getDetectedTextareaCount(): number {
  const count = document.querySelectorAll(LINKEDIN_TEXTAREA_SELECTORS.join(', ')).length;
  return count;
}
