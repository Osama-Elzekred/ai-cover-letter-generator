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
let isProcessing = false;
let isCollapsed = true; // Default to closed

// Prompt State
let cvPromptEnabled = false;
let clPromptEnabled = false;
let cvPromptMode: 0 | 1 = 0; // 0: Append, 1: Override
let clPromptMode: 0 | 1 = 0; // Default to Append now, user can switch

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
  if (isProcessing) {
    return `
      <div class="processing-view">
        <div class="spinner"></div>
        <p>AI is working its magic...</p>
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
        <p>${analysisSummary}</p>
      </div>

      <div style="display: flex; gap: 10px; align-items: center;">
        <button id="ai-reanalyze-btn" class="secondary-btn">Re-analyze</button>
        ${selectedKeywords.length > 0 ? `
          <button id="ai-bridge-cv-btn" class="primary-btn ai-grad animate-fade-in" style="font-size: 12px; padding: 8px 20px; width: auto; margin: 0;">
            Build Resume with ${selectedKeywords.length} confirmed skills →
          </button>
        ` : ''}
      </div>
    </div>
  `;
}

function renderResumeTab(): string {
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
          <textarea id="cv-custom-prompt" placeholder="e.g. Focus on my cloud experience or keep it under 1 page..."></textarea>
        </div>
      ` : ''}
      
      <div class="action-status-box">
        <div class="status-badge">Professional PDF</div>
        <p>Uses aggressive keyword matching to beat ATS filters.</p>
        <div class="mini-tags">
          <span>ATS-optimized</span>
          <span>Keyword-matched</span>
        </div>
      </div>

      <button id="ai-magic-cv-btn" class="primary-btn ai-grad">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="white" style="margin-right: 8px;"><path d="M12 2L14.5 9L22 11.5L14.5 14L12 21L9.5 14L2 11.5L9.5 9L12 2Z"/></svg>
        Generate CV
      </button>
    </div>
  `;
}

function renderLetterTab(): string {
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

  // Bridge Button
  const bridgeBtn = container.querySelector('#ai-bridge-cv-btn');
  if (bridgeBtn) bridgeBtn.addEventListener('click', () => {
    activeTab = 'resume';
    renderWidget(container);
  });

  // Collapse Toggle
  const collapseBtn = container.querySelector('#ai-collapse-btn');
  if (collapseBtn) collapseBtn.addEventListener('click', () => {
    isCollapsed = !isCollapsed;
    container.classList.toggle('collapsed', isCollapsed);
    renderWidget(container);
  });
}

// --- Action Handlers ---

async function handleAnalyze() {
  const jobData = extractJobData();
  if (!jobData) return;

  isProcessing = true;
  renderWidget(document.getElementById('ai-copilot-container')!);

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
    isProcessing = false;
    renderWidget(document.getElementById('ai-copilot-container')!);
  }
}

async function handleMagicCV() {
  const jobData = extractJobData();
  if (!jobData) return;

  const customPrompt = (document.getElementById('cv-custom-prompt') as HTMLTextAreaElement)?.value;
  
  isProcessing = true;
  renderWidget(document.getElementById('ai-copilot-container')!);

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
    
    if (response?.type !== 'SUCCESS') {
        alert(response?.error || 'Generation failed.');
    }
  } catch (err) {
    alert('Error generating CV.');
  } finally {
    isProcessing = false;
    renderWidget(document.getElementById('ai-copilot-container')!);
  }
}

async function handleCoverLetter() {
  const jobData = extractJobData();
  if (!jobData) return;

  const customPrompt = (document.getElementById('cl-custom-prompt') as HTMLTextAreaElement)?.value;
  
  isProcessing = true;
  renderWidget(document.getElementById('ai-copilot-container')!);

  try {
    const response = await chrome.runtime.sendMessage({ 
      type: 'GENERATE_COVER_LETTER_DIRECT', 
      payload: { 
        ...jobData, 
        customPromptTemplate: clPromptEnabled ? customPrompt : null,
        promptMode: clPromptMode
      } 
    });
    
    if (response?.type !== 'SUCCESS') {
        alert(response?.error || 'Generation failed.');
    }
  } catch (err) {
    alert('Error generating Cover Letter.');
  } finally {
    isProcessing = false;
    renderWidget(document.getElementById('ai-copilot-container')!);
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
