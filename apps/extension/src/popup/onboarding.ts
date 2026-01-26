// ============================================
// User Onboarding Tutorial Module
// ============================================

import * as storage from '../utils/storage.js';

export interface OnboardingStep {
  id: number;
  title: string;
  description: string;
  highlightSelector?: string;
  action?: 'none' | 'open-link' | 'focus-element' | 'switch-tab';
  actionData?: string;
  position?: 'center' | 'top' | 'bottom' | 'left' | 'right';
}

const ONBOARDING_STEPS: OnboardingStep[] = [
  {
    id: 1,
    title: '👋 Welcome to AI Job Co-Pilot',
    description: 'Your intelligent assistant for generating professional cover letters and customizing your CV for any job posting. Let\'s get you set up in just a few steps!',
    position: 'center',
  },
  {
    id: 2,
    title: '📄 Upload Your CV',
    description: 'Start by uploading your CV (PDF or DOCX) or paste the text directly. This allows the AI to generate personalized content based on your experience.',
    highlightSelector: '#cvUploadArea',
    position: 'bottom',
  },
  {
    id: 3,
    title: '🔑 Get Your Free Groq API Key',
    description: '<strong>Important:</strong> Without an API key, you\'re limited to <strong>10 requests per minute</strong> (shared across all users).<br><br><strong>With your own free API key:</strong><br>✅ Unlimited requests<br>✅ Faster response times<br>✅ Priority access<br>✅ Private & secure (key stays on your device)<br><br>Groq offers a generous free tier with <strong>10,000+ requests per day</strong>. Click below to get your key in 30 seconds:',
    position: 'center',
    action: 'open-link',
    actionData: 'https://console.groq.com/keys',
  },
  {
    id: 4,
    title: '💾 Save Your API Key',
    description: 'Once you have your Groq API key (starts with <code>gsk_</code>), go to the Settings tab and paste it here. Your key is stored locally and never shared.',
    highlightSelector: '#apiKeyInput',
    action: 'switch-tab',
    actionData: 'settings',
    position: 'bottom',
  },
  {
    id: 5,
    title: '🎯 Auto-Detect Jobs on LinkedIn',
    description: 'Browse LinkedIn jobs and click "Auto-Detect from LinkedIn" to automatically extract job details. The extension will fill in the job title, company, and description for you!',
    highlightSelector: '#extractJobBtn',
    action: 'switch-tab',
    actionData: 'input',
    position: 'bottom',
  },
  {
    id: 6,
    title: '✨ Generate Your First Cover Letter',
    description: 'Fill in the job details (or use auto-detect), then click "Generate Cover Letter" to create a personalized, professional cover letter. You can also use "Magic AI CV Tailor" to customize your resume!',
    highlightSelector: '#generateBtn',
    position: 'bottom',
  },
];

export class OnboardingManager {
  private currentStep: number = 0;
  private modal: HTMLElement | null = null;
  private spotlight: HTMLElement | null = null;
  private onComplete: (() => void) | null = null;

  constructor() {
    this.createModal();
  }

  /**
   * Check if user has completed onboarding
   */
  static async hasCompletedOnboarding(): Promise<boolean> {
    const completed = await storage.getOnboardingCompleted();
    return completed === true;
  }

  /**
   * Mark onboarding as completed
   */
  static async markCompleted(): Promise<void> {
    await storage.setOnboardingCompleted(true);
  }

  /**
   * Reset onboarding (for "Restart Tutorial" feature)
   */
  static async reset(): Promise<void> {
    await storage.setOnboardingCompleted(false);
  }

  /**
   * Start the onboarding flow
   */
  async start(onComplete?: () => void): Promise<void> {
    this.onComplete = onComplete || null;
    this.currentStep = 0;
    this.showStep(0);
    document.body.classList.add('onboarding-active');
  }

  /**
   * Show a specific step
   */
  private showStep(stepIndex: number): void {
    if (stepIndex >= ONBOARDING_STEPS.length) {
      this.complete();
      return;
    }

    const step = ONBOARDING_STEPS[stepIndex];
    this.currentStep = stepIndex;

    // Update modal content
    this.updateModalContent(step);

    // Show modal
    if (this.modal) {
      this.modal.classList.remove('hidden');
      this.modal.classList.add('visible');
    }

    // Handle spotlight highlighting
    if (step.highlightSelector) {
      this.highlightElement(step.highlightSelector);
    } else {
      this.removeSpotlight();
    }

    // Execute step action
    this.executeAction(step);
  }

  /**
   * Update modal content with step data
   */
  private updateModalContent(step: OnboardingStep): void {
    if (!this.modal) return;

    const title = this.modal.querySelector('.onboarding-title') as HTMLElement;
    const description = this.modal.querySelector('.onboarding-description') as HTMLElement;
    const progressDots = this.modal.querySelector('.progress-dots') as HTMLElement;
    const stepCounter = this.modal.querySelector('.step-counter') as HTMLElement;
    const actionBtn = this.modal.querySelector('.onboarding-action-btn') as HTMLButtonElement;

    if (title) title.textContent = step.title;
    if (description) description.innerHTML = step.description;
    if (stepCounter) stepCounter.textContent = `Step ${step.id} of ${ONBOARDING_STEPS.length}`;

    // Update progress dots
    if (progressDots) {
      progressDots.innerHTML = ONBOARDING_STEPS.map((_, index) => 
        `<span class="progress-dot ${index === this.currentStep ? 'active' : ''} ${index < this.currentStep ? 'completed' : ''}"></span>`
      ).join('');
    }

    // Show/hide action button for Step 3 (Groq API link)
    if (actionBtn) {
      if (step.id === 3) {
        actionBtn.classList.remove('hidden');
        actionBtn.textContent = '🚀 Get Free API Key';
      } else {
        actionBtn.classList.add('hidden');
      }
    }

    // Update navigation buttons
    const prevBtn = this.modal.querySelector('.btn-prev') as HTMLButtonElement;
    const nextBtn = this.modal.querySelector('.btn-next') as HTMLButtonElement;
    
    if (prevBtn) {
      prevBtn.disabled = this.currentStep === 0;
    }

    if (nextBtn) {
      nextBtn.textContent = this.currentStep === ONBOARDING_STEPS.length - 1 ? 'Finish' : 'Next';
    }
  }

  /**
   * Highlight a specific element with spotlight effect
   */
  private highlightElement(selector: string): void {
    const element = document.querySelector(selector) as HTMLElement;
    if (!element) {
      this.removeSpotlight();
      return;
    }

    // Create or update spotlight
    if (!this.spotlight) {
      this.spotlight = document.createElement('div');
      this.spotlight.className = 'onboarding-spotlight';
      document.body.appendChild(this.spotlight);
    }

    // Calculate element position
    const rect = element.getBoundingClientRect();
    const padding = 8;

    this.spotlight.style.top = `${rect.top - padding}px`;
    this.spotlight.style.left = `${rect.left - padding}px`;
    this.spotlight.style.width = `${rect.width + padding * 2}px`;
    this.spotlight.style.height = `${rect.height + padding * 2}px`;
    this.spotlight.classList.add('visible');

    // Add highlight class to element
    element.classList.add('onboarding-highlight');

    // Scroll element into view if needed
    element.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  /**
   * Remove spotlight effect
   */
  private removeSpotlight(): void {
    if (this.spotlight) {
      this.spotlight.classList.remove('visible');
    }

    // Remove highlight class from all elements
    document.querySelectorAll('.onboarding-highlight').forEach(el => {
      el.classList.remove('onboarding-highlight');
    });
  }

  /**
   * Execute step-specific actions
   */
  private executeAction(step: OnboardingStep): void {
    if (!step.action || step.action === 'none') return;

    switch (step.action) {
      case 'open-link':
        // Link will be opened by the action button click
        break;

      case 'switch-tab':
        if (step.actionData === 'settings') {
          const settingsTab = document.getElementById('tabSettings') as HTMLButtonElement;
          if (settingsTab) {
            // Switch to settings tab after a short delay
            setTimeout(() => settingsTab.click(), 500);
          }
        } else if (step.actionData === 'input') {
          const inputTab = document.getElementById('tabInput') as HTMLButtonElement;
          if (inputTab) {
            setTimeout(() => inputTab.click(), 500);
          }
        }
        break;

      case 'focus-element':
        if (step.actionData) {
          const element = document.querySelector(step.actionData) as HTMLElement;
          if (element && 'focus' in element) {
            setTimeout(() => element.focus(), 500);
          }
        }
        break;
    }
  }

  /**
   * Navigate to next step
   */
  next(): void {
    if (this.currentStep < ONBOARDING_STEPS.length - 1) {
      this.showStep(this.currentStep + 1);
    } else {
      this.complete();
    }
  }

  /**
   * Navigate to previous step
   */
  prev(): void {
    if (this.currentStep > 0) {
      this.showStep(this.currentStep - 1);
    }
  }

  /**
   * Skip onboarding
   */
  skip(): void {
    this.complete();
  }

  /**
   * Complete onboarding
   */
  private async complete(): Promise<void> {
    // Hide modal and remove spotlight
    if (this.modal) {
      this.modal.classList.remove('visible');
      setTimeout(() => this.modal?.classList.add('hidden'), 300);
    }
    this.removeSpotlight();
    document.body.classList.remove('onboarding-active');

    // Mark as completed
    await OnboardingManager.markCompleted();

    // Call completion callback
    if (this.onComplete) {
      this.onComplete();
    }
  }

  /**
   * Create modal HTML structure
   */
  private createModal(): void {
    const modal = document.createElement('div');
    modal.id = 'onboardingModal';
    modal.className = 'onboarding-modal hidden';
    modal.innerHTML = `
      <div class="onboarding-backdrop"></div>
      <div class="onboarding-content">
        <div class="onboarding-header">
          <h2 class="onboarding-title">Welcome</h2>
          <button class="onboarding-skip" aria-label="Skip tutorial">Skip</button>
        </div>
        
        <div class="onboarding-body">
          <p class="onboarding-description">Description</p>
          <button class="onboarding-action-btn btn btn-primary hidden">Action Button</button>
        </div>
        
        <div class="onboarding-footer">
          <div class="onboarding-progress">
            <span class="step-counter">Step 1 of 6</span>
            <div class="progress-dots"></div>
          </div>
          
          <div class="onboarding-nav">
            <button class="btn btn-secondary btn-prev">Previous</button>
            <button class="btn btn-primary btn-next">Next</button>
          </div>
        </div>
      </div>
    `;

    document.body.appendChild(modal);
    this.modal = modal;

    // Attach event listeners
    this.attachEventListeners();
  }

  /**
   * Attach event listeners to modal buttons
   */
  private attachEventListeners(): void {
    if (!this.modal) return;

    const skipBtn = this.modal.querySelector('.onboarding-skip') as HTMLButtonElement;
    const prevBtn = this.modal.querySelector('.btn-prev') as HTMLButtonElement;
    const nextBtn = this.modal.querySelector('.btn-next') as HTMLButtonElement;
    const actionBtn = this.modal.querySelector('.onboarding-action-btn') as HTMLButtonElement;
    const backdrop = this.modal.querySelector('.onboarding-backdrop') as HTMLElement;

    skipBtn?.addEventListener('click', () => this.skip());
    prevBtn?.addEventListener('click', () => this.prev());
    nextBtn?.addEventListener('click', () => this.next());
    backdrop?.addEventListener('click', () => this.skip());

    actionBtn?.addEventListener('click', () => {
      const currentStep = ONBOARDING_STEPS[this.currentStep];
      if (currentStep.action === 'open-link' && currentStep.actionData) {
        window.open(currentStep.actionData, '_blank');
      }
    });
  }

  /**
   * Clean up resources
   */
  destroy(): void {
    if (this.modal) {
      this.modal.remove();
      this.modal = null;
    }
    if (this.spotlight) {
      this.spotlight.remove();
      this.spotlight = null;
    }
    document.body.classList.remove('onboarding-active');
  }
}

/**
 * Initialize onboarding if needed
 */
export async function initOnboarding(onComplete?: () => void): Promise<void> {
  const hasCompleted = await OnboardingManager.hasCompletedOnboarding();
  
  if (!hasCompleted) {
    const manager = new OnboardingManager();
    await manager.start(onComplete);
  }
}

/**
 * Manually restart onboarding tutorial
 */
export async function restartOnboarding(onComplete?: () => void): Promise<void> {
  await OnboardingManager.reset();
  const manager = new OnboardingManager();
  await manager.start(onComplete);
}
