// ============================================
// Textarea Answer Modal & UI
// ============================================

interface TextareaAnswerRequest {
  fieldLabel: string;
  userQuestion: string;
  // Optional: extractJobDataIfAvailable
}

interface TextareaAnswerResponse {
  answer: string;
  fieldLabel: string;
}

/**
 * Create and show modal for textarea question answering
 */
export function showTextareaModal(fieldLabel: string, onConfirm?: (answer: string) => void): void {
  // Remove existing modal if present
  const existing = document.getElementById('ai-textarea-modal-overlay');
  if (existing) existing.remove();

  // Create modal overlay
  const overlay = document.createElement('div');
  overlay.id = 'ai-textarea-modal-overlay';
  overlay.innerHTML = `
    <div style="
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 9999;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    ">
      <div style="
        background: white;
        border-radius: 12px;
        box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
        width: 90%;
        max-width: 500px;
        padding: 32px;
        max-height: 80vh;
        overflow-y: auto;
      ">
        <h2 style="
          margin: 0 0 8px 0;
          font-size: 20px;
          font-weight: 600;
          color: #000;
        ">
          Generate Answer with AI
        </h2>
        
        <p style="
          margin: 0 0 24px 0;
          color: #666;
          font-size: 14px;
        ">
          Ask a question about: <strong>${fieldLabel}</strong>
        </p>

        <div style="margin-bottom: 20px;">
          <label style="
            display: block;
            font-size: 13px;
            font-weight: 500;
            margin-bottom: 8px;
            color: #333;
          ">
            Your Question:
          </label>
          <textarea 
            id="ai-textarea-question-input"
            placeholder="e.g., Why are you interested in this role? How do your skills match the requirements?"
            style="
              width: 100%;
              min-height: 100px;
              padding: 12px;
              border: 1px solid #ddd;
              border-radius: 6px;
              font-size: 14px;
              font-family: inherit;
              resize: vertical;
              box-sizing: border-box;
            "
          ></textarea>
        </div>

        <div style="margin-bottom: 24px;">
          <label style="
            display: flex;
            align-items: center;
            cursor: pointer;
            font-size: 13px;
            color: #333;
          ">
            <input 
              id="ai-textarea-use-job-context" 
              type="checkbox" 
              checked="checked"
              style="margin-right: 8px; cursor: pointer;"
            />
            Include current job context (if available)
          </label>
        </div>

        <div style="
          display: flex;
          gap: 12px;
          justify-content: flex-end;
        ">
          <button 
            id="ai-textarea-modal-cancel"
            style="
              padding: 10px 20px;
              border: 1px solid #ddd;
              border-radius: 6px;
              background: white;
              color: #333;
              cursor: pointer;
              font-size: 14px;
              font-weight: 500;
              transition: all 0.2s ease;
            "
          >
            Cancel
          </button>
          <button 
            id="ai-textarea-modal-generate"
            style="
              padding: 10px 20px;
              border: none;
              border-radius: 6px;
              background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
              color: white;
              cursor: pointer;
              font-size: 14px;
              font-weight: 500;
              transition: all 0.2s ease;
            "
          >
            Generate Answer
          </button>
        </div>

        <div id="ai-textarea-modal-status" style="
          margin-top: 16px;
          padding: 12px;
          border-radius: 6px;
          font-size: 13px;
          display: none;
        "></div>
      </div>
    </div>
  `;

  document.body.appendChild(overlay);

  // Wire up event listeners
  const cancelBtn = document.getElementById('ai-textarea-modal-cancel');
  const generateBtn = document.getElementById('ai-textarea-modal-generate') as HTMLButtonElement;
  const questionInput = document.getElementById('ai-textarea-question-input') as HTMLTextAreaElement;
  const useJobContext = document.getElementById('ai-textarea-use-job-context') as HTMLInputElement;
  const statusDiv = document.getElementById('ai-textarea-modal-status');

  cancelBtn?.addEventListener('click', () => {
    overlay.remove();
  });

  generateBtn?.addEventListener('click', async () => {
    const question = questionInput.value.trim();
    if (!question) {
      showStatus(statusDiv!, 'Please enter a question', 'error');
      return;
    }

    generateBtn.disabled = true;
    generateBtn.textContent = 'Generating...';

    try {
      const response = await chrome.runtime.sendMessage({
        type: 'GENERATE_TEXTAREA_ANSWER',
        payload: {
          fieldLabel,
          userQuestion: question,
          includeJobContext: useJobContext.checked,
        },
      });

      if (response.error) {
        showStatus(statusDiv!, response.error, 'error');
        generateBtn.disabled = false;
        generateBtn.textContent = 'Generate Answer';
        return;
      }

      // Show generated answer
      showAnswerPreview(overlay, response.answer, fieldLabel, () => {
        if (onConfirm) onConfirm(response.answer);
        overlay.remove();
      });
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : 'Failed to generate answer';
      showStatus(statusDiv!, errorMsg, 'error');
      generateBtn.disabled = false;
      generateBtn.textContent = 'Generate Answer';
    }
  });

  // Focus on question input
  setTimeout(() => questionInput?.focus(), 100);
}

/**
 * Show generated answer preview
 */
function showAnswerPreview(
  overlay: HTMLElement,
  answer: string,
  fieldLabel: string,
  onInsert: () => void
): void {
  const modalContent = overlay.querySelector('[style*="background: white"]') as HTMLElement;
  if (!modalContent) return;

  modalContent.innerHTML = `
    <h2 style="
      margin: 0 0 8px 0;
      font-size: 20px;
      font-weight: 600;
      color: #000;
    ">
      Generated Answer
    </h2>
    
    <p style="
      margin: 0 0 16px 0;
      color: #666;
      font-size: 13px;
    ">
      For: <strong>${fieldLabel}</strong>
    </p>

    <div style="
      background: #f9f9f9;
      border: 1px solid #ddd;
      border-radius: 6px;
      padding: 16px;
      margin-bottom: 24px;
      max-height: 300px;
      overflow-y: auto;
      font-size: 14px;
      line-height: 1.6;
      color: #333;
      white-space: pre-wrap;
      word-break: break-word;
    ">
      ${escapeHtml(answer)}
    </div>

    <div style="
      display: flex;
      gap: 12px;
      justify-content: flex-end;
    ">
      <button 
        id="ai-textarea-preview-edit"
        style="
          padding: 10px 20px;
          border: 1px solid #ddd;
          border-radius: 6px;
          background: white;
          color: #333;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s ease;
        "
      >
        Edit
      </button>
      <button 
        id="ai-textarea-preview-copy"
        style="
          padding: 10px 20px;
          border: 1px solid #ddd;
          border-radius: 6px;
          background: white;
          color: #333;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s ease;
        "
      >
        Copy
      </button>
      <button 
        id="ai-textarea-preview-insert"
        style="
          padding: 10px 20px;
          border: none;
          border-radius: 6px;
          background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
          color: white;
          cursor: pointer;
          font-size: 14px;
          font-weight: 500;
          transition: all 0.2s ease;
        "
      >
        Insert Answer
      </button>
    </div>
  `;

  const editBtn = modalContent.querySelector('#ai-textarea-preview-edit');
  const copyBtn = modalContent.querySelector('#ai-textarea-preview-copy');
  const insertBtn = modalContent.querySelector('#ai-textarea-preview-insert');

  editBtn?.addEventListener('click', () => {
    showTextareaModal(fieldLabel, onInsert);
  });

  copyBtn?.addEventListener('click', () => {
    navigator.clipboard.writeText(answer).then(() => {
      const originalText = (copyBtn as HTMLButtonElement).textContent;
      (copyBtn as HTMLButtonElement).textContent = 'Copied!';
      setTimeout(() => {
        (copyBtn as HTMLButtonElement).textContent = originalText;
      }, 2000);
    });
  });

  insertBtn?.addEventListener('click', onInsert);
}

/**
 * Show status message in modal
 */
function showStatus(statusDiv: HTMLElement, message: string, type: 'error' | 'success' | 'info'): void {
  const bgColor = type === 'error' ? '#fee' : type === 'success' ? '#efe' : '#eef';
  const textColor = type === 'error' ? '#c33' : type === 'success' ? '#3c3' : '#33c';

  statusDiv.style.background = bgColor;
  statusDiv.style.color = textColor;
  statusDiv.style.display = 'block';
  statusDiv.textContent = message;
}

/**
 * Escape HTML to prevent injection
 */
function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
