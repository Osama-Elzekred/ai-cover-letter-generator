/**
 * Popup Settings Panel - Logging Configuration Controls
 * 
 * Add these controls to your extension popup settings UI
 * for users to toggle logging at runtime.
 * 
 * Usage in HTML:
 * ```html
 * <div id="logging-settings" class="settings-panel">
 *   <div class="setting">
 *     <label>
 *       <input type="checkbox" id="logging-enabled" />
 *       Enable Extension Logging
 *     </label>
 *   </div>
 *   
 *   <div class="setting">
 *     <label>Log Level:</label>
 *     <select id="log-level">
 *       <option value="debug">Debug (Most Verbose)</option>
 *       <option value="info">Info</option>
 *       <option value="warn">Warning</option>
 *       <option value="error">Error Only</option>
 *     </select>
 *   </div>
 *   
 *   <div class="setting">
 *     <label>
 *       <input type="checkbox" id="verbose-mode" />
 *       Verbose Console Output (Dev Only)
 *     </label>
 *   </div>
 *   
 *   <div class="actions">
 *     <button id="log-config-show">Show Config</button>
 *     <button id="log-config-reset">Reset to Defaults</button>
 *   </div>
 * </div>
 * ```
 */

import { LoggingManager, type LogLevel } from '../config/logging-config.js';

export class LoggingSettingsPanel {
  private initialized = false;

  /**
   * Initialize popup logging controls
   * Call this when popup loads
   */
  async init(): Promise<void> {
    if (this.initialized) return;

    // Load current config
    const config = await LoggingManager.getConfig();

    // Setup toggle: logging enabled
    const enabledCheckbox = document.getElementById('logging-enabled') as HTMLInputElement;
    if (enabledCheckbox) {
      enabledCheckbox.checked = config.enabled;
      enabledCheckbox.addEventListener('change', async (e) => {
        const target = e.target as HTMLInputElement;
        await LoggingManager.setEnabled(target.checked);
        this.showNotification(target.checked ? 'Logging enabled' : 'Logging disabled');
      });
    }

    // Setup dropdown: log level
    const levelSelect = document.getElementById('log-level') as HTMLSelectElement;
    if (levelSelect) {
      levelSelect.value = config.level;
      levelSelect.addEventListener('change', async (e) => {
        const target = e.target as HTMLSelectElement;
        await LoggingManager.setLogLevel(target.value as LogLevel);
        this.showNotification(`Log level set to ${target.value}`);
      });
    }

    // Setup toggle: verbose mode
    const verboseCheckbox = document.getElementById('verbose-mode') as HTMLInputElement;
    if (verboseCheckbox) {
      verboseCheckbox.checked = config.verbose ?? false;
      verboseCheckbox.addEventListener('change', async (e) => {
        const target = e.target as HTMLInputElement;
        await LoggingManager.setVerbose(target.checked);
        this.showNotification(target.checked ? 'Verbose mode enabled' : 'Verbose mode disabled');
      });
    }

    // Setup button: show config
    const showConfigBtn = document.getElementById('log-config-show') as HTMLButtonElement;
    if (showConfigBtn) {
      showConfigBtn.addEventListener('click', async () => {
        const config = await LoggingManager.getConfig();
        const message = `
Logging Config:
- Enabled: ${config.enabled}
- Level: ${config.level}
- Verbose: ${config.verbose}
- Source: ${config.source}

Check browser console for full details.`;
        alert(message);
        await LoggingManager.printConfig();
      });
    }

    // Setup button: reset to defaults
    const resetBtn = document.getElementById('log-config-reset') as HTMLButtonElement;
    if (resetBtn) {
      resetBtn.addEventListener('click', async () => {
        if (confirm('Reset logging config to build-time defaults?')) {
          await LoggingManager.reset();
          location.reload(); // Refresh to show updated values
          this.showNotification('Config reset to defaults');
        }
      });
    }

    this.initialized = true;
  }

  /**
   * Show user notification (toast-like message)
   */
  private showNotification(message: string): void {
    const notification = document.createElement('div');
    notification.className = 'logging-notification';
    notification.textContent = message;
    notification.style.cssText = `
      position: fixed;
      bottom: 10px;
      left: 10px;
      background: #4CAF50;
      color: white;
      padding: 10px 15px;
      border-radius: 4px;
      font-size: 12px;
      z-index: 10000;
    `;

    document.body.appendChild(notification);

    setTimeout(() => {
      notification.remove();
    }, 3000);
  }
}

/**
 * Auto-initialize on popup load
 */
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', async () => {
    const panel = new LoggingSettingsPanel();
    await panel.init();
  });
} else {
  const panel = new LoggingSettingsPanel();
  panel.init().catch(console.error);
}
