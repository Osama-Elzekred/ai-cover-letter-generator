// ============================================
// LinkedIn AI Co-Pilot - Premium Integrated UI
// ============================================

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

interface MatchResult {
  matchScore: number;
  matchingKeywords: string[];
  missingKeywords: string[];
  analysisSummary: string;
}

// Global State
let selectedKeywords: string[] = [];
let currentMatchData: MatchResult | null = null;
let activeTab: 'match' | 'resume' | 'letter' = 'match';
let isCollapsed = true; // Default to closed

// Per-tab Processing State
let matchProcessing = false;
let matchProcessingStep = '';
let cvProcessing = false;
let cvProcessingStep = '';
let letterProcessing = false;
let letterProcessingStep = '';

// Prompt State
let cvPromptEnabled = false;
let clPromptEnabled = false;
let cvPromptMode: 0 | 1 = 0; // 0: Append, 1: Override
let clPromptMode: 0 | 1 = 0; // Default to Append now, user can switch

// Result State
let isCvReady = false;
let isLetterReady = false;
let lastGeneratedCv: any = null;
let lastGeneratedLetter: any = null;

/**
 * Extract job data from LinkedIn job posting page
 */
function extractJobData(): JobData | null {
  try {
    const jobTitleElement = document.querySelector(
      '.job-details-jobs-unified-top-card__job-title, .jobs-unified-top-card__job-title, h1.t-24'
    );
    const jobTitle = jobTitleElement?.textContent?.trim() || '';

    const companyElement = document.querySelector(
      '.job-details-jobs-unified-top-card__company-name, .jobs-unified-top-card__company-name, .job-details-jobs-unified-top-card__primary-description a'
    );
    const companyName = companyElement?.textContent?.trim() || '';

    const descriptionElement = document.querySelector(
      '.jobs-description__content, .jobs-box__html-content, .jobs-description'
    );
    const jobDescription = descriptionElement?.textContent?.trim() || '';

    if (!jobTitle || !companyName || !jobDescription) return null;

    return { jobTitle, companyName, jobDescription };
  } catch (error) {
    return null;
  }
}

/**
 * Main Injection Entry Point
 */
function injectCoPilotWidget() {
  if (document.getElementById('ai-copilot-container')) return;

  const parent = document.querySelector('.job-details-jobs-unified-top-card__container--two-pane');
  if (!parent) return;

  const container = document.createElement('div');
  container.id = 'ai-copilot-container';
  container.className = 'ai-copilot-wrapper' + (isCollapsed ? ' collapsed' : '');
  
  // Inject Styles
  injectStyles();

  renderWidget(container);
  parent.appendChild(container);
  
  console.log('[AI Co-Pilot] Widget Injected');
}

function renderWidget(container: HTMLElement) {
  const jobData = extractJobData();
  const summaryText = jobData ? `Tailored for ${jobData.jobTitle} at ${jobData.companyName}` : "AI-powered job assistance";

  container.innerHTML = `
    <div class="glass-card shadow-premium">
      <!-- Card Header -->
      <div class="card-header">
        <div class="header-main">
          <div class="ai-icon-box">
             <svg viewBox="0 0 24 24" width="20" height="20" fill="white"><path d="M12 2L14.5 9L22 11.5L14.5 14L12 21L9.5 14L2 11.5L9.5 9L12 2Z"/></svg>
          </div>
          <div class="header-text">
            <h3>AI Job Co-Pilot</h3>
            <p>${summaryText}</p>
          </div>
        </div>
        
        <div class="header-actions">
          <div class="collapsed-icons">
            <button class="mini-icon-btn" title="Match" data-goto="match">
              <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M18 20V10M12 20V4M6 20v-6"></path></svg>
            </button>
            <button class="mini-icon-btn" title="Resume" data-goto="resume">
              <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"></path><path d="M14 2v6h6"></path><path d="M16 13H8M16 17H8M10 9H8"></path></svg>
            </button>
            <button class="mini-icon-btn" title="Cover Letter" data-goto="letter">
              <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path><polyline points="22,6 12,13 2,6"></polyline></svg>
            </button>
          </div>
          <button id="ai-collapse-btn" class="icon-btn">
            <svg viewBox="0 0 24 24" width="18" height="18" stroke="currentColor" fill="none" stroke-width="2" class="chevron-icon"><path d="M18 15l-6-6-6 6"/></svg>
          </button>
        </div>
      </div>

      <!-- Tab Navigation -->
      <div class="tab-nav">
        <button class="tab-btn ${activeTab === 'match' ? 'active' : ''}" data-tab="match">
           <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" fill="none" class="tab-icon"><path d="M18 20V10M12 20V4M6 20v-6"></path></svg>
           Match
        </button>
        <button class="tab-btn ${activeTab === 'resume' ? 'active' : ''}" data-tab="resume">
           <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" fill="none" class="tab-icon"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"></path><path d="M14 2v6h6"></path><path d="M16 13H8M16 17H8M10 9H8"></path></svg>
           Resume
        </button>
        <button class="tab-btn ${activeTab === 'letter' ? 'active' : ''}" data-tab="letter">
           <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" fill="none" class="tab-icon"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path><polyline points="22,6 12,13 2,6"></polyline></svg>
           Letter
        </button>
      </div>

      <!-- Content Area -->
      <div id="ai-tab-content" class="tab-content">
        ${renderCurrentTab()}
      </div>
    </div>
  `;

  attachEvents(container);
}

function renderCurrentTab(): string {
  // Check if current active tab is processing
  const isProcessing = 
    (activeTab === 'match' && matchProcessing) ||
    (activeTab === 'resume' && cvProcessing) ||
    (activeTab === 'letter' && letterProcessing);
  
  const processingStep = 
    activeTab === 'match' ? matchProcessingStep :
    activeTab === 'resume' ? cvProcessingStep :
    letterProcessingStep;

  if (isProcessing) {
    return `
      <div class="processing-view">
        <div class="spinner"></div>
        <p class="processing-text animate-pulse">${processingStep}</p>
      </div>
    `;
  }

  switch(activeTab) {
    case 'match': return renderMatchTab();
    case 'resume': return renderResumeTab();
    case 'letter': return renderLetterTab();
    default: return '';
  }
}

function renderMatchTab(): string {
  if (!currentMatchData) {
    return `
      <div class="info-view">
        <p class="desc" style="margin-bottom: 10px;">Instantly analyze how your skills align with this job posting.</p>
        <ul class="feature-list">
          <li>Keyword gap analysis</li>
          <li>Skills alignment score</li>
          <li>Actionable insights</li>
        </ul>
        <button id="ai-analyze-btn" class="primary-btn pulse">
          <svg viewBox="0 0 24 24" width="16" height="16" fill="white" style="margin-right: 8px;"><path d="M12 2L14.5 9L22 11.5L14.5 14L12 21L9.5 14L2 11.5L9.5 9L12 2Z"/></svg>
          Analyze Match
        </button>
      </div>
    `;
  }

  const { matchScore, matchingKeywords, missingKeywords, analysisSummary } = currentMatchData;
  const circumference = 2 * Math.PI * 32;
  const offset = circumference - (matchScore / 100) * circumference;

  return `
    <div class="match-ready-view">
      <div class="match-header">
        <div class="score-container" style="position: relative; width: 100px; height: 100px; margin-right: 24px;">
          <svg width="100" height="100" viewBox="0 0 100 100" class="score-svg" style="position: absolute; top: 0; left: 0;">
            <circle cx="50" cy="50" r="42" class="score-bg" />
            <circle cx="50" cy="50" r="42" class="score-fill" style="stroke-dasharray: ${2 * Math.PI * 42}; stroke-dashoffset: ${(1 - matchScore / 100) * 2 * Math.PI * 42};" />
          </svg>
          <div class="score-text" style="position: absolute; width: 100%; top: 50%; left: 0; transform: translateY(-50%); text-align: center; display: flex; flex-direction: column; align-items: center; justify-content: center; background: none; border: none; padding: 0; margin: 0;">
            <span class="pct" style="font-size: 22px; font-weight: 800; color: #1e293b; line-height: 1; display: block; margin: 0; border: none; background: none;">${matchScore}%</span>
            <span class="label" style="font-size: 10px; font-weight: 700; color: #64748b; margin-top: 4px; line-height: 1; display: block; white-space: nowrap; border: none; background: none;">${matchScore > 80 ? 'Great Match' : matchScore > 50 ? 'Good Match' : 'Potential'}</span>
          </div>
        </div>
        
        <div class="keywords-summary">
          <div class="kw-group">
            <h4>Found (${matchingKeywords.length})</h4>
            <div class="kw-cloud">
               ${matchingKeywords.map(k => `<span class="chip chip-found">✓ ${k}</span>`).join('')}
               ${matchingKeywords.length === 0 ? '<span class="empty">No direct matches found.</span>' : ''}
            </div>
          </div>
          <div class="kw-group">
            <h4>Missing (${missingKeywords.length})</h4>
            <div class="kw-cloud">
               ${missingKeywords.map(k => `
                 <button class="chip chip-missing ${selectedKeywords.includes(k) ? 'selected' : ''}" data-kw="${k}">
                   ${selectedKeywords.includes(k) ? '✓ ' : 'ⓘ '}${k}
                 </button>
               `).join('')}
               ${missingKeywords.length === 0 ? '<span class="empty">All keywords matched!</span>' : ''}
            </div>
          </div>
        </div>
      </div>
      
      <div class="ai-insight">
        <span class="badge">AI Insight</span>
        <p>${analysisSummary.replace(/\n/g, '<br/>')}</p>
      </div>

      <div style="display: flex; gap: 10px; align-items: center;">
        <button id="ai-reanalyze-btn" class="secondary-btn">Re-analyze</button>
        ${selectedKeywords.length > 0 ? `
          <button id="ai-bridge-cv-btn" class="primary-btn ai-grad animate-fade-in" style="font-size: 12px; padding: 8px 20px; width: auto; margin: 0;">
            ${lastGeneratedCv ? 'Update Resume' : 'Build Resume'} with ${selectedKeywords.length} confirmed skills →
          </button>
        ` : ''}
        ${currentMatchData ? `
          <button id="ai-dismiss-match-btn" class="text-btn danger-hover" style="font-size: 10px; margin-left: auto;">
             Dismiss Match
          </button>
        ` : ''}
      </div>
    </div>
  `;
}

function renderResumeTab(): string {
  if (isCvReady && lastGeneratedCv) {
    return `
      <div class="editor-view-container animate-fade-in">
        <div class="editor-header" style="justify-content: space-between; margin-bottom: 10px;">
           <h4 style="font-size: 14px; font-weight: 700; color: #1e293b; margin: 0;">✏️ LaTeX Editor</h4>
           <button id="widget-dismiss-cv-btn" class="text-btn danger-hover" style="font-size: 10px;">Dismiss</button>
        </div>

        <div class="widget-editor-container">
          <pre class="widget-editor-highlighting" id="widget-highlighting"><code class="language-latex" id="widget-highlighting-content"></code></pre>
          <textarea class="widget-editor-textarea" id="widget-latex-source" spellcheck="false">${lastGeneratedCv.latexSource}</textarea>
        </div>
        <div class="widget-editor-actions">
          <button id="widget-recompile-btn" class="primary-btn ai-grad" style="flex: 2;">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="white" stroke-width="2" style="margin-right: 6px;"><path d="M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>
            Re-compile
          </button>
          <button id="widget-view-browser-btn" class="secondary-btn" title="Preview in Browser" style="padding: 6px 12px;">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>
          </button>
          <button id="widget-download-btn" class="secondary-btn" title="Download PDF" style="padding: 6px 12px;">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"></path></svg>
          </button>
          <button id="widget-overleaf-btn" class="secondary-btn overleaf-btn" title="Open in Overleaf" style="color: white; border: none;">
             Overleaf
          </button>
        </div>
        <p class="hint-text" style="margin-top: 12px;">💡 Edit LaTeX, preview instantly, or export to Overleaf for full control.</p>
      </div>
    `;
  }

  return `
    <div class="action-view">
      <div class="prompt-toggle-row">
        <span>Enable Custom Instructions</span>
        <label class="switch">
          <input type="checkbox" id="cv-prompt-toggle" ${cvPromptEnabled ? 'checked' : ''}>
          <span class="slider"></span>
        </label>
      </div>

      ${cvPromptEnabled ? `
        <div class="instruction-box animate-fade-in">
          <div class="mode-selector">
             <button class="mode-btn ${cvPromptMode === 0 ? 'active' : ''}" data-mode="0">Append</button>
             <button class="mode-btn ${cvPromptMode === 1 ? 'active' : ''}" data-mode="1">Override</button>
          </div>
          <div style="font-size: 11px; color: #64748b; margin: 8px 0; line-height: 1.4;">
            <strong>Append:</strong> Add extra instructions to default prompt (LaTeX template unchanged)<br>
            <strong>Override:</strong> Use only the custom prompt you type here (ignores Settings)
            ${cvPromptMode === 1 ? `
            <div style="margin-top: 8px; padding: 8px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px;">
              <div style="font-weight: 600; color: #334155; font-size: 11px; margin-bottom: 6px;">Required placeholders (Override):</div>
              <pre style="margin: 0; font-size: 11px; color: #334155; white-space: pre-wrap;">
JOB DESCRIPTION:
{JobDescription}

CANDIDATE INFORMATION:
{CvText}

{ConfirmedSkills}

LATEX STRUCTURE TEMPLATE:
(put your LaTeX template code here)
              </pre>
            </div>
            ` : ''}
          </div>
          <textarea id="cv-custom-prompt" placeholder="e.g. Focus on my cloud experience or keep it under 1 page..."></textarea>
        </div>
      ` : ''}
      
      <div class="action-status-box" style="margin-top: 16px;">
        <div class="status-badge">Professional PDF</div>
        <p>Uses aggressive keyword matching to beat ATS filters.</p>
        <div class="mini-tags">
          <span>ATS-optimized</span>
          <span>Keyword-matched</span>
        </div>
      </div>

      <button id="ai-magic-cv-btn" class="primary-btn ai-grad" style="width: 100%;">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="white" style="margin-right: 8px;"><path d="M12 2L14.5 9L22 11.5L14.5 14L12 21L9.5 14L2 11.5L9.5 9L12 2Z"/></svg>
        Generate CV
      </button>
      
      <p style="margin: 12px 0 0 0; font-size: 11px; color: #94a3b8; text-align: center;">
        💡 Tip: Customize prompts via extension Settings tab (click extension icon)
      </p>
    </div>
  `;
}

function renderLetterTab(): string {
  if (isLetterReady && lastGeneratedLetter) {
    return `
      <div class="letter-view-container animate-fade-in">
        <div class="letter-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
          <h4 style="font-size: 14px; font-weight: 700; color: #1e293b; margin: 0;">📝 Your Cover Letter</h4>
          <button id="widget-dismiss-cl-btn" class="text-btn danger-hover" style="font-size: 10px;">Dismiss</button>
        </div>
        
        <div class="letter-textarea-wrapper" style="position: relative;">
          <textarea 
            id="widget-letter-content" 
            class="letter-textarea" 
            spellcheck="true" 
            style="width: 100%; height: 350px; padding: 16px; border: 1px solid #e2e8f0; border-radius: 8px; font-family: -apple-system, system-ui, sans-serif; font-size: 13px; line-height: 1.6; color: #1e293b; resize: vertical; box-sizing: border-box;"
          >${lastGeneratedLetter.coverLetter}</textarea>
        </div>
        
        <div class="letter-actions" style="display: flex; gap: 10px; margin-top: 12px;">
          <button id="widget-copy-cl-btn" class="primary-btn cl-grad" style="flex: 1;">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="white" stroke-width="2" style="margin-right: 6px;"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
            Copy to Clipboard
          </button>
          <button id="widget-download-cl-btn" class="secondary-btn" title="Download as TXT" style="padding: 8px 16px;">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"></path></svg>
          </button>
          <button id="widget-regenerate-cl-btn" class="secondary-btn" style="padding: 8px 16px;">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>
          </button>
        </div>
        
        <p class="hint-text" style="margin-top: 12px; font-size: 11px; color: #94a3b8; font-style: italic;">💡 Edit the text above before copying or downloading.</p>
      </div>
    `;
  }

  return `
    <div class="action-view">
      <div class="prompt-toggle-row">
        <span>Enable Personalization Prompt</span>
        <label class="switch">
          <input type="checkbox" id="cl-prompt-toggle" ${clPromptEnabled ? 'checked' : ''}>
          <span class="slider"></span>
        </label>
      </div>

      ${clPromptEnabled ? `
        <div class="instruction-box animate-fade-in">
          <div class="mode-selector">
             <button class="mode-btn ${clPromptMode === 0 ? 'active' : ''}" data-mode="0">Append</button>
             <button class="mode-btn ${clPromptMode === 1 ? 'active' : ''}" data-mode="1">Override</button>
          </div>
          <div style="font-size: 11px; color: #64748b; margin: 8px 0; line-height: 1.4;">
            <strong>Append:</strong> Add extra instructions to default prompt<br>
            <strong>Override:</strong> Use only the custom prompt you type here (ignores Settings)
            ${clPromptMode === 1 ? `
            <div style="margin-top: 8px; padding: 8px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px;">
              <div style="font-weight: 600; color: #334155; font-size: 11px; margin-bottom: 6px;">Required placeholders (Override):</div>
              <pre style="margin: 0; font-size: 11px; color: #334155; white-space: pre-wrap;">
JOB DESCRIPTION:
{JobDescription}

CANDIDATE'S CV:
{CvText}

              </pre>
            </div>
            ` : ''}
          </div>
          <textarea id="cl-custom-prompt" placeholder="e.g. Mention I worked with their lead engineer before..."></textarea>
        </div>
      ` : ''}
      
      <div class="action-status-box">
        <div class="status-badge">Personalized TXT</div>
        <p>Narrative-driven letter that connects your history to their future.</p>
      </div>

      <button id="ai-cover-letter-btn" class="primary-btn cl-grad">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="white" style="margin-right: 8px;"><path d="M12 2L14.5 9L22 11.5L14.5 14L12 21L9.5 14L2 11.5L9.5 9L12 2Z"/></svg>
        Generate Letter
      </button>
    </div>
  `;
}

function attachEvents(container: HTMLElement) {
  // Tab Switching
  container.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      activeTab = (btn as HTMLElement).dataset.tab as any;
      isCollapsed = false;
      container.classList.remove('collapsed');
      renderWidget(container);
    });
  });

  // Collapsed Quick Access
  container.querySelectorAll('.mini-icon-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      activeTab = (btn as HTMLElement).dataset.goto as any;
      isCollapsed = false;
      container.classList.remove('collapsed');
      renderWidget(container);
    });
  });

  // Keyword Toggling
  container.querySelectorAll('.chip-missing').forEach(btn => {
    btn.addEventListener('click', () => {
      const kw = (btn as HTMLElement).dataset.kw!;
      if (selectedKeywords.includes(kw)) {
        selectedKeywords = selectedKeywords.filter(k => k !== kw);
      } else {
        selectedKeywords.push(kw);
      }
      renderWidget(container);
    });
  });

  // Prompt Toggles
  const cvToggle = container.querySelector('#cv-prompt-toggle') as HTMLInputElement;
  if (cvToggle) cvToggle.addEventListener('change', () => {
    cvPromptEnabled = cvToggle.checked;
    renderWidget(container);
  });

  const clToggle = container.querySelector('#cl-prompt-toggle') as HTMLInputElement;
  if (clToggle) clToggle.addEventListener('change', () => {
    clPromptEnabled = clToggle.checked;
    renderWidget(container);
  });

  // LaTeX Template Toggle (removed - now in Settings)
  // Users can customize prompts including LaTeX templates via extension Settings tab

  // Mode Selectors
  container.querySelectorAll('.mode-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const mode = parseInt((btn as HTMLElement).dataset.mode!) as 0 | 1;
      if (activeTab === 'resume') cvPromptMode = mode;
      else clPromptMode = mode;
      renderWidget(container);
    });
  });

  // Analyze Action
  const analyzeBtn = container.querySelector('#ai-analyze-btn');
  if (analyzeBtn) analyzeBtn.addEventListener('click', handleAnalyze);

  const reanalyzeBtn = container.querySelector('#ai-reanalyze-btn');
  if (reanalyzeBtn) reanalyzeBtn.addEventListener('click', handleAnalyze);

  // CV Action
  const cvBtn = container.querySelector('#ai-magic-cv-btn');
  if (cvBtn) cvBtn.addEventListener('click', handleMagicCV);

  // CL Action
  const clBtn = container.querySelector('#ai-cover-letter-btn');
  if (clBtn) clBtn.addEventListener('click', handleCoverLetter);

  // Bridge Button (Update/Build Resume)
  const bridgeBtn = container.querySelector('#ai-bridge-cv-btn');
  if (bridgeBtn) bridgeBtn.addEventListener('click', () => {
    if (lastGeneratedCv) {
      const confirmUpdate = window.confirm("Regenerating the CV will overwrite your manual edits. Continue?");
      if (!confirmUpdate) return;
      isCvReady = false; // Reset to show the 'Generate' screen
    }
    activeTab = 'resume';
    renderWidget(container);
  });

  // Dismiss Match Button
  const dismissMatchBtn = container.querySelector('#ai-dismiss-match-btn');
  if (dismissMatchBtn) {
    dismissMatchBtn.addEventListener('click', () => {
       currentMatchData = null;
       renderWidget(container);
    });
  }

  // Dismiss Resume Button
  const dismissCvBtn = container.querySelector('#widget-dismiss-cv-btn');
  if (dismissCvBtn) {
    dismissCvBtn.addEventListener('click', () => {
       lastGeneratedCv = null;
       isCvReady = false;
       renderWidget(container);
    });
  }

  // Collapse Toggle
  const collapseBtn = container.querySelector('#ai-collapse-btn');
  if (collapseBtn) collapseBtn.addEventListener('click', () => {
    isCollapsed = !isCollapsed;
    container.classList.toggle('collapsed', isCollapsed);
    renderWidget(container);
  });

  // Editor Sync and Actions
  const latexSource = container.querySelector('#widget-latex-source') as HTMLTextAreaElement;
  if (latexSource) {
    // Initial highlighting
    updateWidgetEditor(container);

    latexSource.addEventListener('input', () => {
      updateWidgetEditor(container);
    });

    latexSource.addEventListener('scroll', () => {
      const highlighting = container.querySelector('#widget-highlighting') as HTMLElement;
      if (highlighting) {
        highlighting.scrollTop = latexSource.scrollTop;
        highlighting.scrollLeft = latexSource.scrollLeft;
      }
    });
  }

  const recompileBtn = container.querySelector('#widget-recompile-btn');
  if (recompileBtn) recompileBtn.addEventListener('click', handleWidgetRecompile);

  const downloadBtn = container.querySelector('#widget-download-btn');
  if (downloadBtn) downloadBtn.addEventListener('click', async () => {
      if (!lastGeneratedCv) return;
      
      // Get original CV filename from storage
      const cvData = await chrome.storage.local.get(['cvFileName']);
      const originalName = cvData.cvFileName ? cvData.cvFileName.replace(/\.[^/.]+$/, '') : 'resume';
      
      // Sanitize job title for filename
      const jobTitle = lastGeneratedCv.jobTitle || 'tailored';
      const sanitizedJobTitle = jobTitle
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '_')
        .replace(/^_+|_+$/g, '')
        .substring(0, 50); // Limit length
      
      const filename = `${originalName}_${sanitizedJobTitle}.pdf`;
      downloadFile(lastGeneratedCv.pdfContent, filename, 'application/pdf');
  });

  const viewBrowserBtn = container.querySelector('#widget-view-browser-btn');
  if (viewBrowserBtn) viewBrowserBtn.addEventListener('click', () => {
      if (!lastGeneratedCv) return;
      const byteCharacters = atob(lastGeneratedCv.pdfContent);
      const byteNumbers = new Array(byteCharacters.length);
      for (let i = 0; i < byteCharacters.length; i++) byteNumbers[i] = byteCharacters.charCodeAt(i);
      const byteArray = new Uint8Array(byteNumbers);
      const blob = new Blob([byteArray], { type: 'application/pdf' });
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
  });

  const overleafBtn = container.querySelector('#widget-overleaf-btn');
  if (overleafBtn) overleafBtn.addEventListener('click', handleWidgetOverleaf);

  // Cover Letter Actions
  const copyClBtn = container.querySelector('#widget-copy-cl-btn');
  if (copyClBtn) copyClBtn.addEventListener('click', handleCopyLetter);

  const downloadClBtn = container.querySelector('#widget-download-cl-btn');
  if (downloadClBtn) downloadClBtn.addEventListener('click', () => {
    if (!lastGeneratedLetter) return;
    const textarea = container.querySelector('#widget-letter-content') as HTMLTextAreaElement;
    const content = textarea ? textarea.value : lastGeneratedLetter.coverLetter;
    
    // Sanitize job title for filename
    const jobTitle = lastGeneratedLetter.jobTitle || 'cover_letter';
    const sanitizedJobTitle = jobTitle
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '')
      .substring(0, 50);
    
    const filename = `cover_letter_${sanitizedJobTitle}.txt`;
    downloadFile(btoa(unescape(encodeURIComponent(content))), filename, 'text/plain');
  });

  const regenerateClBtn = container.querySelector('#widget-regenerate-cl-btn');
  if (regenerateClBtn) regenerateClBtn.addEventListener('click', () => {
    if (window.confirm('Regenerate the cover letter? Your current edits will be lost.')) {
      lastGeneratedLetter = null;
      isLetterReady = false;
      renderWidget(container);
    }
  });

  const dismissClBtn = container.querySelector('#widget-dismiss-cl-btn');
  if (dismissClBtn) dismissClBtn.addEventListener('click', () => {
    lastGeneratedLetter = null;
    isLetterReady = false;
    renderWidget(container);
  });
}

function updateWidgetEditor(container: HTMLElement) {
  const textarea = container.querySelector('#widget-latex-source') as HTMLTextAreaElement;
  const highlightingContent = container.querySelector('#widget-highlighting-content') as HTMLElement;
  if (!textarea || !highlightingContent) return;

  let code = textarea.value;
  if (code[code.length-1] == "\n") code += " ";
  highlightingContent.textContent = code;
  
  // Use Prism if available
  const anyGlobal = (globalThis as any);
  const prism = anyGlobal.Prism || (window as any).Prism;
  
  if (prism) {
    if (prism.languages && prism.languages.latex) {
      console.log('[AI Co-Pilot] Prism & LaTeX detected, highlighting...');
      prism.highlightElement(highlightingContent);
    } else {
      console.warn('[AI Co-Pilot] Prism found but LaTeX grammar is missing!', prism.languages);
      // Fallback: if latex is missing, maybe it's under another name or not loaded
    }
  } else {
    console.error('[AI Co-Pilot] Prism library NOT found in content script scope.');
  }

  // Sync to Storage so Popup is updated too
  if (lastGeneratedCv) {
    lastGeneratedCv.latexSource = textarea.value;
    chrome.storage.local.set({
      editorState: {
        latex: textarea.value,
        pdfBase64: lastGeneratedCv.pdfContent,
        updatedAt: Date.now()
      }
    });
  }
}

async function handleWidgetRecompile() {
  const container = document.getElementById('ai-copilot-container')!;
  const textarea = container.querySelector('#widget-latex-source') as HTMLTextAreaElement;
  if (!textarea || !lastGeneratedCv) return;

  const source = textarea.value.trim();
  cvProcessing = true;
  
  const steps = [
    { text: 'Validating LaTeX syntax...', duration: 200 },
    { text: 'Compiling with pdfLaTeX...', duration: 0 }
  ];

  for (let i = 0; i < steps.length; i++) {
    cvProcessingStep = steps[i].text;
    renderWidget(container);
    if (i < steps.length - 1) await new Promise(r => setTimeout(r, steps[i].duration));
  }

  try {
    const response = await chrome.runtime.sendMessage({ 
      type: 'COMPILE_LATEX_DIRECT', 
      payload: { latexSource: source } 
    });

    if (response?.type === 'SUCCESS') {
      lastGeneratedCv.pdfContent = response.payload.pdfContent;
      lastGeneratedCv.latexSource = source;
      
      // Update persistent storage
      await chrome.storage.local.set({
        editorState: {
          latex: source,
          pdfBase64: response.payload.pdfContent,
          updatedAt: Date.now()
        }
      });
    } else {
      alert(response?.error || 'Re-compile failed.');
    }
  } catch (err) {
    alert('Error connecting to compiler.');
  } finally {
    cvProcessing = false;
    renderWidget(container);
  }
}

function handleWidgetOverleaf() {
  const container = document.getElementById('ai-copilot-container')!;
  const textarea = container.querySelector('#widget-latex-source') as HTMLTextAreaElement;
  if (!textarea) return;

  const source = textarea.value;
  chrome.runtime.sendMessage({
    type: 'OPEN_OVERLEAF_DIRECT',
    payload: { latexSource: source }
  });
}

async function handleCopyLetter() {
  const container = document.getElementById('ai-copilot-container')!;
  const textarea = container.querySelector('#widget-letter-content') as HTMLTextAreaElement;
  const copyBtn = container.querySelector('#widget-copy-cl-btn');
  
  if (!textarea || !copyBtn) return;

  try {
    await navigator.clipboard.writeText(textarea.value);
    
    // Visual feedback
    const originalHTML = copyBtn.innerHTML;
    copyBtn.innerHTML = `
      <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="white" stroke-width="2" style="margin-right: 6px;"><path d="M20 6L9 17l-5-5"></path></svg>
      Copied!
    `;
    copyBtn.classList.add('success-pulse');
    
    setTimeout(() => {
      copyBtn.innerHTML = originalHTML;
      copyBtn.classList.remove('success-pulse');
    }, 2000);
  } catch (err) {
    alert('Failed to copy to clipboard. Please copy manually.');
  }
}

function downloadFile(base64: string, fileName: string, type: string) {
  const byteCharacters = atob(base64);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

// --- Action Handlers ---

async function handleAnalyze() {
  const jobData = extractJobData();
  if (!jobData) return;

  matchProcessing = true;
  const container = document.getElementById('ai-copilot-container')!;
  
  const steps = [
    { text: 'Extracting job requirements...', duration: 300 },
    { text: 'Loading your CV data...', duration: 200 },
    { text: 'Analyzing with AI...', duration: 2000 },
    { text: 'Calculating match score...', duration: 0 }
  ];

  for (let i = 0; i < steps.length; i++) {
    matchProcessingStep = steps[i].text;
    renderWidget(container);
    if (i < steps.length - 1) await new Promise(r => setTimeout(r, steps[i].duration));
  }

  try {
    const response = await chrome.runtime.sendMessage({ 
      type: 'MATCH_CV_DIRECT', 
      payload: jobData 
    });
    
    if (response?.type === 'SUCCESS') {
      currentMatchData = response.payload;
    } else {
      alert(response?.error || 'Analysis failed. Make sure server is running.');
    }
  } catch (err) {
    alert('Failed to connect to Extension. Please refresh page.');
  } finally {
    matchProcessing = false;
    renderWidget(container);
  }
}

async function handleMagicCV() {
  const jobData = extractJobData();
  if (!jobData) return;

  const customPrompt = (document.getElementById('cv-custom-prompt') as HTMLTextAreaElement)?.value;
  
  // Note: Custom LaTeX templates are now managed via extension Settings tab
  // Users save custom prompts (which can include LaTeX templates) persistently
  
  cvProcessing = true;
  const container = document.getElementById('ai-copilot-container')!;
  
  const steps = [
    { text: 'Extracting job requirements...', duration: 300 },
    { text: 'Loading your CV...', duration: 200 },
    { text: 'Tailoring content with AI...', duration: 2500 },
    { text: 'Injecting keywords...', duration: 300 },
    { text: 'Compiling to PDF...', duration: 0 }
  ];

  for (let i = 0; i < steps.length; i++) {
    cvProcessingStep = steps[i].text;
    renderWidget(container);
    if (i < steps.length - 1) await new Promise(r => setTimeout(r, steps[i].duration));
  }

  try {
    const response = await chrome.runtime.sendMessage({ 
      type: 'CUSTOMIZE_CV_DIRECT', 
      payload: { 
        ...jobData, 
        selectedKeywords,
        customPromptTemplate: cvPromptEnabled ? customPrompt : null,
        promptMode: cvPromptMode
      } 
    });
    
    if (response?.type === 'SUCCESS') {
        isCvReady = true;
        lastGeneratedCv = response.payload;
        lastGeneratedCv.jobTitle = jobData.jobTitle; // Store for filename
    } else {
        alert(response?.error || 'Generation failed.');
    }
  } catch (err) {
    alert('Error generating CV.');
  } finally {
    cvProcessing = false;
    renderWidget(container);
  }
}

async function handleCoverLetter() {
  const jobData = extractJobData();
  if (!jobData) return;

  const customPrompt = (document.getElementById('cl-custom-prompt') as HTMLTextAreaElement)?.value;
  
  letterProcessing = true;
  const container = document.getElementById('ai-copilot-container')!;
  
  const steps = [
    { text: 'Extracting job details...', duration: 300 },
    { text: 'Analyzing your CV...', duration: 200 },
    { text: 'Crafting personalized letter with AI...', duration: 2500 },
    { text: 'Finalizing content...', duration: 0 }
  ];

  for (let i = 0; i < steps.length; i++) {
    letterProcessingStep = steps[i].text;
    renderWidget(container);
    if (i < steps.length - 1) await new Promise(r => setTimeout(r, steps[i].duration));
  }

  try {
    const response = await chrome.runtime.sendMessage({ 
      type: 'GENERATE_COVER_LETTER_DIRECT', 
      payload: { 
        ...jobData, 
        customPromptTemplate: clPromptEnabled ? customPrompt : null,
        promptMode: clPromptMode
      } 
    });
    
    if (response?.type === 'SUCCESS') {
        isLetterReady = true;
        lastGeneratedLetter = response.payload;
        lastGeneratedLetter.jobTitle = jobData.jobTitle; // Store for filename
    } else {
        alert(response?.error || 'Generation failed.');
    }
  } catch (err) {
    alert('Error generating Cover Letter.');
  } finally {
    letterProcessing = false;
    renderWidget(container);
  }
}

async function handleViewPrompt() {
  try {
    // Request templates from service worker
    const response = await chrome.runtime.sendMessage({ 
      type: 'VIEW_PROMPTS_DIRECT'
    });
    
    if (response?.type !== 'SUCCESS') {
      throw new Error(response?.error || 'Failed to load templates');
    }
    
    const templates = response.payload;
    
    // Create modal to display templates
    const modal = document.createElement('div');
    modal.style.cssText = `
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: rgba(0, 0, 0, 0.7);
      z-index: 999999;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
    `;
    
    modal.innerHTML = `
      <div style="background: white; border-radius: 12px; max-width: 800px; max-height: 90vh; overflow: auto; padding: 24px; box-shadow: 0 20px 50px rgba(0,0,0,0.3);">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
          <h2 style="margin: 0; color: #1e293b; font-size: 18px; font-weight: 700;">📋 Prompt Templates</h2>
          <button id="close-modal-btn" style="background: none; border: none; font-size: 24px; cursor: pointer; color: #64748b;">&times;</button>
        </div>
        
        <div style="margin-bottom: 20px;">
          <h3 style="color: #475569; font-size: 14px; font-weight: 600; margin-bottom: 8px;">🎯 CV Customization Prompt</h3>
          <pre style="background: #f8fafc; padding: 16px; border-radius: 8px; font-size: 11px; line-height: 1.6; overflow-x: auto; color: #334155; border: 1px solid #e2e8f0;">${templates.cvCustomization}</pre>
        </div>
        
        <div style="margin-bottom: 20px;">
          <h3 style="color: #475569; font-size: 14px; font-weight: 600; margin-bottom: 8px;">💌 Cover Letter Prompt</h3>
          <pre style="background: #f8fafc; padding: 16px; border-radius: 8px; font-size: 11px; line-height: 1.6; overflow-x: auto; color: #334155; border: 1px solid #e2e8f0;">${templates.coverLetter}</pre>
        </div>
        
        <div style="margin-bottom: 0;">
          <h3 style="color: #475569; font-size: 14px; font-weight: 600; margin-bottom: 8px;">🔍 Match Analysis Prompt</h3>
          <pre style="background: #f8fafc; padding: 16px; border-radius: 8px; font-size: 11px; line-height: 1.6; overflow-x: auto; color: #334155; border: 1px solid #e2e8f0;">${templates.matchAnalysis}</pre>
        </div>
        
        <p style="margin-top: 16px; font-size: 11px; color: #94a3b8; font-style: italic;">💡 Variables like {{CV}}, {{JOB_DESCRIPTION}}, etc. are replaced with actual data at runtime.</p>
      </div>
    `;
    
    document.body.appendChild(modal);
    
    // Close modal on button click or backdrop click
    const closeBtn = modal.querySelector('#close-modal-btn');
    closeBtn?.addEventListener('click', () => modal.remove());
    modal.addEventListener('click', (e) => {
      if (e.target === modal) modal.remove();
    });
    
  } catch (err) {
    alert('Error loading prompt templates. Please try again.');
    console.error(err);
  }
}

function injectStyles() {
  if (document.getElementById('ai-copilot-styles')) return;

  const style = document.createElement('style');
  style.id = 'ai-copilot-styles';
  style.textContent = `
    .ai-copilot-wrapper {
      margin-top: 24px;
      margin-bottom: 24px;
      max-width: 768px;
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    
    .glass-card {
      background: white;
      border: 1px solid rgba(226, 232, 240, 1);
      border-radius: 12px;
      overflow: hidden;
      font-family: -apple-system, system-ui, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    
    /* Collapsed State */
    .ai-copilot-wrapper.collapsed .tab-nav,
    .ai-copilot-wrapper.collapsed .tab-content {
      display: none;
    }

    .ai-copilot-wrapper.collapsed .chevron-icon {
      transform: rotate(180deg);
    }

    .ai-copilot-wrapper.collapsed .collapsed-icons {
      display: flex;
    }

    .collapsed-icons {
      display: none;
      align-items: center;
      gap: 6px;
      margin-right: 12px;
      padding-right: 12px;
      border-right: 1px solid #f1f5f9;
    }

    .mini-icon-btn {
      width: 32px;
      height: 32px;
      border-radius: 6px;
      border: none;
      background: #f8fafc;
      color: #64748b;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s;
    }

    .mini-icon-btn:hover {
      background: #f1f5f9;
      color: #7c3aed;
    }

    .header-actions {
      display: flex;
      align-items: center;
    }

    .feature-list {
      list-style: none !important;
      padding: 0;
      margin: 0 0 20px 0;
      display: flex;
      flex-direction: row;
      justify-content: center;
      gap: 12px;
      flex-wrap: wrap;
    }

    .feature-list li {
      font-size: 11px;
      color: #64748b;
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: 99px;
    }

    .feature-list li::before {
      content: "●";
      color: #5d5bd4;
      font-size: 8px;
    }

    /* Standard Elements */
    .card-header {
      padding: 10px 20px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: white;
    }

    .header-main {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .ai-icon-box {
      width: 36px;
      height: 36px;
      background: #5d5bd4;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 12px rgba(93, 91, 212, 0.2);
    }

    .header-text h3 {
      margin: 0;
      font-size: 14px;
      font-weight: 700;
      color: #1e293b;
    }

    .header-text p {
      margin: 0;
      font-size: 11px;
      color: #64748b;
    }

    .icon-btn {
      width: 32px;
      height: 32px;
      border-radius: 6px;
      border: none;
      background: #f8fafc;
      color: #94a3b8;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
    }

    .icon-btn:hover {
      background: #f1f5f9;
      color: #1e293b;
    }

    /* Tab Nav */
    .tab-nav {
      display: flex;
      padding: 0 10px;
      background: white;
      border-top: 1px solid #f1f5f9;
      border-bottom: 1px solid #f1f5f9;
    }

    .tab-btn {
      flex: 1;
      padding: 12px 0;
      border: none;
      background: transparent;
      font-size: 13px;
      font-weight: 600;
      color: #64748b;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      border-bottom: 2px solid transparent;
      transition: all 0.2s;
    }

    .tab-btn.active {
      color: #5d5bd4;
      border-bottom-color: #5d5bd4;
      background: white;
    }

    /* Match Styles */
    .tab-content { padding: 20px; }
    
    .match-header { display: flex; gap: 30px; align-items: flex-start; }
    
    .score-container { position: relative; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .score-svg { transform: rotate(-90deg); }
    .score-bg { fill: none; stroke: rgba(0,0,0,0.03); stroke-width: 6; }
    .score-fill { fill: none; stroke: #5d5bd4; stroke-width: 6; stroke-linecap: round; }
    
    .score-text { position: absolute; text-align: center; width: 100%; top: 50%; transform: translateY(-50%); }
    .score-text .pct { display: block; font-size: 20px; font-weight: 800; color: #1e293b; line-height: 1; }
    .score-text .label { font-size: 10px; font-weight: 700; color: #64748b; line-height: 1; margin-top: 2px; }

    .kw-group h4 { font-size: 11px; font-weight: 700; color: #64748b; margin: 0 0 8px 0; }
    .kw-cloud { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 16px; }
    
    .chip { font-size: 11px; padding: 4px 10px; border-radius: 99px; border: 1px solid transparent; transition: all 0.2s; }
    .chip-found { background: #eefdf5; color: #059669; border-color: #d1fae5; }
    .chip-missing { background: #fff7ed; color: #c2410c; border-color: #ffedd5; cursor: pointer; }
    .chip-missing.selected { background: #5d5bd4; color: white; border-color: #5d5bd4; }

    .ai-insight { background: white; border: 1px solid #f1f5f9; padding: 12px; border-radius: 8px; margin-bottom: 16px; font-size: 13px; }
    .ai-insight .badge { color: #5d5bd4; font-weight: 700; display: block; font-size: 11px; margin-bottom: 4px; }

    /* Action Styles */
    .prompt-toggle-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 15px;
      padding: 10px 0;
      border-bottom: 1px dashed #e2e8f0;
    }

    .prompt-toggle-row span { font-size: 13px; font-weight: 600; color: #475569; }

    .switch { position: relative; display: inline-block; width: 36px; height: 20px; }
    .switch input { opacity: 0; width: 0; height: 0; }
    .slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background-color: #cbd5e1; transition: .4s; border-radius: 34px; }
    .slider:before { position: absolute; content: ""; height: 14px; width: 14px; left: 3px; bottom: 3px; background-color: white; transition: .4s; border-radius: 50%; }
    input:checked + .slider { background-color: #5d5bd4; }
    input:checked + .slider:before { transform: translateX(16px); }

    .mode-selector { display: flex; gap: 4px; background: #f1f5f9; padding: 3px; border-radius: 8px; margin-bottom: 10px; }
    .mode-btn { flex: 1; border: none; background: transparent; padding: 6px; font-size: 11px; font-weight: 700; color: #64748b; cursor: pointer; border-radius: 6px; transition: all 0.2s; }
    .mode-btn.active { background: white; color: #1e293b; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }

    .instruction-box textarea { width: 100%; height: 80px; padding: 12px; border: 1px solid #e2e8f0; border-radius: 8px; font-family: inherit; font-size: 13px; resize: none; outline: none; transition: border-color 0.2s; }
    .instruction-box textarea:focus { border-color: #5d5bd4; }

    .action-status-box { background: #f5f3ff; padding: 14px; border-radius: 10px; margin: 15px 0; }
    .status-badge { font-size: 10px; font-weight: 800; color: #5d5bd4; text-transform: uppercase; margin-bottom: 5px; }
    .action-status-box p { margin: 0; font-size: 12px; color: #4338ca; }
    .mini-tags { display: flex; gap: 8px; margin-top: 8px; }
    .mini-tags span { font-size: 10px; color: #6366f1; background: white; padding: 2px 8px; border-radius: 4px; border: 1px solid #e0e7ff; }

    .primary-btn { width: 100%; padding: 12px; border-radius: 8px; border: none; background: #5d5bd4; color: white; font-weight: 700; font-size: 14px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; }
    .primary-btn:hover { background: #4e4cb8; transform: translateY(-1px); }

    .secondary-btn { background: white; border: 1px solid #e2e8f0; color: #64748b; padding: 8px 16px; border-radius: 8px; font-size: 12px; font-weight: 600; cursor: pointer; transition: all 0.2s; }
    .secondary-btn:hover { background: #f8fafc; color: #1e293b; }

    .animate-fade-in { animation: fadeIn 0.3s ease-out; }
    @keyframes fadeIn { from { opacity: 0; transform: translateY(5px); } to { opacity: 1; transform: translateY(0); } }

    .spinner { width: 30px; height: 30px; border: 3px solid #f1f5f9; border-top-color: #5d5bd4; border-radius: 50%; margin: 20px auto; animation: spin 1s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .processing-view { text-align: center; padding: 20px; }
    .processing-view p { font-size: 13px; color: #64748b; }
    .animate-pulse { animation: pulse 1.5s ease-in-out infinite; }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.5; } }

    .success-pulse { animation: successPulse 0.4s ease; }
    @keyframes successPulse { 
      0% { transform: scale(1); } 
      50% { transform: scale(1.05); background: #10b981; } 
      100% { transform: scale(1); } 
    }

    /* Success View */
    .success-view { text-align: center; padding: 10px 0; }
    .success-icon-box { margin-bottom: 12px; }
    .success-view h4 { font-size: 16px; font-weight: 700; color: #1e293b; margin: 0 0 8px 0; }
    .success-view p { font-size: 13px; color: #64748b; line-height: 1.5; margin-bottom: 20px; }
    .success-actions { display: flex; gap: 10px; margin-bottom: 15px; }
    .hint-text { font-size: 11px; color: #94a3b8; font-style: italic; }

    /* Embedded Editor Styles */
    .editor-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .editor-tabs { display: flex; background: #f1f5f9; padding: 3px; border-radius: 20px; }
    .editor-tab-btn { padding: 4px 12px; border: none; background: transparent; font-size: 11px; font-weight: 700; cursor: pointer; border-radius: 16px; color: #64748b; }
    .editor-tab-btn.active { background: white; color: #1e293b; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }

    .widget-editor-container { position: relative; height: 300px; background: #1e1e1e; border-radius: 8px; overflow: hidden; border: 1px solid #333; }
    .widget-editor-textarea, .widget-editor-highlighting {
      position: absolute; top: 0; left: 0; width: 100%; height: 100%;
      margin: 0 !important; padding: 16px !important; border: none !important; 
      font-family: 'Consolas', 'Monaco', 'Courier New', monospace !important; 
      font-size: 13px !important; line-height: 1.5 !important;
      white-space: pre !important; 
      word-wrap: normal !important;
      overflow: auto !important; box-sizing: border-box !important;
      text-align: left !important;
    }
    .widget-editor-textarea {
      background: transparent !important; 
      color: transparent !important; 
      -webkit-text-fill-color: transparent !important;
      caret-color: white !important; 
      z-index: 2 !important; 
      resize: none !important; 
      outline: none !important;
    }
    .widget-editor-highlighting { z-index: 1 !important; pointer-events: none !important; color: #ccc !important; }
    .widget-editor-highlighting code { font-family: inherit !important; font-size: inherit !important; line-height: inherit !important; }

    /* Prism Colors Integration - High Specificity */
    #ai-copilot-container .token.comment { color: #999 !important; }
    #ai-copilot-container .token.punctuation { color: #ccc !important; }
    #ai-copilot-container .token.tag, 
    #ai-copilot-container .token.attr-name, 
    #ai-copilot-container .token.namespace { color: #e2777a !important; }
    #ai-copilot-container .token.boolean, 
    #ai-copilot-container .token.number, 
    #ai-copilot-container .token.function { color: #f08d49 !important; }
    #ai-copilot-container .token.property, 
    #ai-copilot-container .token.class-name, 
    #ai-copilot-container .token.constant, 
    #ai-copilot-container .token.symbol { color: #f8c555 !important; }
    #ai-copilot-container .token.selector, 
    #ai-copilot-container .token.important, 
    #ai-copilot-container .token.atrule, 
    #ai-copilot-container .token.keyword, 
    #ai-copilot-container .token.builtin { color: #cc99cd !important; }
    #ai-copilot-container .token.string, 
    #ai-copilot-container .token.char, 
    #ai-copilot-container .token.attr-value, 
    #ai-copilot-container .token.regex, 
    #ai-copilot-container .token.variable { color: #7ec699 !important; }
    #ai-copilot-container .token.operator, 
    #ai-copilot-container .token.entity, 
    #ai-copilot-container .token.url { color: #67cdcc !important; }

    .widget-editor-actions { display: flex; gap: 8px; margin-top: 12px; }
    .widget-preview-box { height: 300px; background: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; padding: 20px; }
    
    .overleaf-btn { background: #47a1ad !important; }
    .btn-icon { margin-right: 6px; }
    
    /* File Upload Styles */
    .drop-zone { 
      display: flex; 
      flex-direction: column; 
      align-items: center; 
      justify-content: center; 
      padding: 32px 20px; 
      background: #f8fafc; 
      border: 2px dashed #cbd5e1; 
      border-radius: 8px; 
      cursor: pointer; 
      transition: all 0.2s ease;
      text-align: center;
    }
    .drop-zone:hover { 
      border-color: #3b82f6; 
      background: #eff6ff; 
    }
    .file-selected {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 12px 16px;
      background: #f0fdf4;
      border: 1px solid #86efac;
      border-radius: 8px;
      font-size: 12px;
      color: #166534;
    }
    .file-selected span {
      flex: 1;
      font-weight: 500;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `;
  document.head.appendChild(style);
}

// --- Observer Logic ---

const observer = new MutationObserver(() => {
  if (window.location.href.includes('linkedin.com/jobs')) {
    injectCoPilotWidget();
  }
});

observer.observe(document.body, { childList: true, subtree: true });
if (window.location.href.includes('linkedin.com/jobs')) injectCoPilotWidget();

/**
 * Handle messages from the popup
 */
chrome.runtime.onMessage.addListener((message: ChromeMessage, sender, sendResponse) => {
  if (message.type === 'EXTRACT_JOB_DATA') {
    const jobData = extractJobData();
    if (jobData) {
      sendResponse({ type: 'JOB_DATA_EXTRACTED', payload: jobData });
    } else {
      sendResponse({ type: 'ERROR', error: 'Could not find job details.' });
    }
  }
  return true;
});
